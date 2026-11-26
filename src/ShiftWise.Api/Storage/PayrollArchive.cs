using System.Globalization;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using ShiftWise.Api.Common;

namespace ShiftWise.Api.Storage;

public record ArchivedExport(string Name, DateTime From, DateTime To, int RowCount, long SizeBytes, DateTimeOffset ArchivedAt);

public record ArchiveResult(ArchivedExport Export, Uri DownloadUrl, DateTimeOffset LinkExpiresAt);

// Keeps a copy of every payroll CSV in Azure Blob Storage so a manager can
// pull last month's file again after the fact.
//
// Layout: one container, one "folder" per company:
//   company-{companyId}/hours_{from}_to_{to}_{archivedAtUtc}.csv
// The timestamp suffix means re-running an export never overwrites the
// copy payroll already received.
public class PayrollArchive
{
    private const string FromKey = "periodfrom";
    private const string ToKey = "periodto";
    private const string RowsKey = "rowcount";
    private const string ArchivedAtKey = "archivedat";

    private readonly BlobContainerClient _container;
    private readonly PayrollArchiveOptions _options;
    private readonly TimeProvider _clock;

    public PayrollArchive(IOptions<PayrollArchiveOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("PayrollArchive:ConnectionString is not set.");
        }

        _container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
    }

    public async Task<ArchiveResult> SaveAsync(int companyId, DateTime from, DateTime to, string csv, int rowCount,
        CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var now = _clock.GetUtcNow();
        var name = $"{Prefix(companyId)}hours_{from:yyyy-MM-dd}_to_{to:yyyy-MM-dd}_{now:yyyyMMdd'T'HHmmssfff'Z'}.csv";
        var blob = _container.GetBlobClient(name);
        var bytes = Encoding.UTF8.GetBytes(csv);

        var upload = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/csv; charset=utf-8" },
            Metadata = new Dictionary<string, string>
            {
                [FromKey] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                [ToKey] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                [RowsKey] = rowCount.ToString(CultureInfo.InvariantCulture),
                // Our own clock, not the blob's CreatedOn, so ordering matches the file names.
                [ArchivedAtKey] = now.ToString("O", CultureInfo.InvariantCulture)
            },
            // Fail instead of silently replacing a blob with the same name.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        };

        using var stream = new MemoryStream(bytes);
        await blob.UploadAsync(stream, upload, ct);

        var export = new ArchivedExport(name, from.Date, to.Date, rowCount, bytes.LongLength, now);
        var (url, expires) = ReadLink(blob);
        return new ArchiveResult(export, url, expires);
    }

    // Newest first. Only ever looks under the caller's company prefix.
    public async Task<List<ArchivedExport>> ListAsync(int companyId, CancellationToken ct = default)
    {
        var results = new List<ArchivedExport>();
        if (!await _container.ExistsAsync(ct))
        {
            return results;
        }

        await foreach (var item in _container.GetBlobsAsync(
                           BlobTraits.Metadata, BlobStates.None, Prefix(companyId), ct))
        {
            results.Add(ToExport(item.Name, item.Metadata, item.Properties.ContentLength ?? 0,
                item.Properties.CreatedOn ?? DateTimeOffset.MinValue));
        }

        return results.OrderByDescending(e => e.ArchivedAt).ToList();
    }

    public async Task<ArchiveResult> GetLinkAsync(int companyId, string name, CancellationToken ct = default)
    {
        // Names come from the URL, so make sure nobody walks into another company's folder.
        if (!name.StartsWith(Prefix(companyId), StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal))
        {
            throw AppException.NotFound("Archived export not found.");
        }

        var blob = _container.GetBlobClient(name);
        try
        {
            var props = await blob.GetPropertiesAsync(cancellationToken: ct);
            var export = ToExport(name, props.Value.Metadata, props.Value.ContentLength, props.Value.CreatedOn);
            var (url, expires) = ReadLink(blob);
            return new ArchiveResult(export, url, expires);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            throw AppException.NotFound("Archived export not found.");
        }
    }

    private static string Prefix(int companyId) => $"company-{companyId}/";

    // Read-only SAS for this one blob. Signed with the account key from the
    // connection string, so it works against Azurite and a real account alike.
    private (Uri Url, DateTimeOffset ExpiresAt) ReadLink(BlobClient blob)
    {
        var expires = _clock.GetUtcNow().AddMinutes(_options.DownloadLinkMinutes);
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, expires)
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            ContentDisposition = $"attachment; filename=\"{Path.GetFileName(blob.Name)}\""
        };
        return (blob.GenerateSasUri(sas), expires);
    }

    private static ArchivedExport ToExport(string name, IDictionary<string, string> metadata, long size, DateTimeOffset created)
    {
        DateTime ParseDate(string key) =>
            metadata.TryGetValue(key, out var v)
                ? DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : DateTime.MinValue;

        var rows = metadata.TryGetValue(RowsKey, out var r) && int.TryParse(r, out var n) ? n : 0;
        var archivedAt = metadata.TryGetValue(ArchivedAtKey, out var a)
                         && DateTimeOffset.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : created;
        return new ArchivedExport(name, ParseDate(FromKey), ParseDate(ToKey), rows, size, archivedAt);
    }
}
