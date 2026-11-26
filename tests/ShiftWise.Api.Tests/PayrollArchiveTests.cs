using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ShiftWise.Api.Common;
using ShiftWise.Api.Storage;

namespace ShiftWise.Api.Tests;

// Runs against a real Blob Storage API: Azurite locally and in CI.
// Start it with `npx azurite-blob --location .azurite` (see README).
// AZURITE_CONNECTION_STRING overrides the default development-storage account.
public class PayrollArchiveTests : IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING") ?? "UseDevelopmentStorage=true";

    private static readonly HttpClient Http = new();

    // Each test class run gets its own container so runs never see each other's blobs.
    private readonly string _containerName = "payroll-test-" + Guid.NewGuid().ToString("N")[..12];
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private static readonly DateTime March = new(2030, 3, 1);
    private static readonly DateTime April = new(2030, 4, 1);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() =>
        await new BlobContainerClient(ConnectionString, _containerName).DeleteIfExistsAsync();

    private PayrollArchive NewArchive(TimeProvider? clock = null) =>
        new(Options.Create(new PayrollArchiveOptions
        {
            ConnectionString = ConnectionString,
            ContainerName = _containerName,
            DownloadLinkMinutes = 15
        }), clock ?? _clock);

    [Fact]
    public async Task Saved_export_downloads_byte_for_byte_through_the_link()
    {
        var csv = "employee_id,employee_name,shift_count,total_hours\n1,\"Lee, Jordan\",2,12.5\n";

        var result = await NewArchive().SaveAsync(7, March, April, csv, rowCount: 1);

        Assert.StartsWith("company-7/hours_2030-03-01_to_2030-04-01_", result.Export.Name);
        Assert.Equal(1, result.Export.RowCount);
        Assert.Equal(Encoding.UTF8.GetByteCount(csv), result.Export.SizeBytes);

        using var response = await Http.GetAsync(result.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(csv, await response.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Download_link_is_read_only()
    {
        var result = await NewArchive().SaveAsync(7, March, April, "a,b\n", rowCount: 0);

        // Same SAS, but try to overwrite the blob with it.
        using var put = new HttpRequestMessage(HttpMethod.Put, result.DownloadUrl)
        {
            Content = new StringContent("tampered")
        };
        put.Headers.Add("x-ms-blob-type", "BlockBlob");
        using var response = await Http.SendAsync(put);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_link_stops_working_after_it_expires()
    {
        // Link signed by a clock an hour behind, so its 15 minutes are already over.
        var lateClock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
        var result = await NewArchive(lateClock).SaveAsync(7, March, April, "a,b\n", rowCount: 0);

        using var response = await Http.GetAsync(result.DownloadUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Archiving_the_same_period_twice_keeps_both_copies_newest_first()
    {
        var archive = NewArchive();
        var first = await archive.SaveAsync(7, March, April, "v1\n", rowCount: 1);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var second = await archive.SaveAsync(7, March, April, "v2\n", rowCount: 2);

        var list = await archive.ListAsync(7);

        Assert.Equal(new[] { second.Export.Name, first.Export.Name }, list.Select(e => e.Name));
        Assert.Equal(2, list[0].RowCount);
        Assert.Equal(March, list[0].From);
        Assert.Equal(April, list[0].To);
    }

    [Fact]
    public async Task Listing_only_shows_the_callers_company()
    {
        var archive = NewArchive();
        await archive.SaveAsync(7, March, April, "mine\n", rowCount: 1);
        await archive.SaveAsync(8, March, April, "theirs\n", rowCount: 1);

        var list = await archive.ListAsync(7);

        var only = Assert.Single(list);
        Assert.StartsWith("company-7/", only.Name);
    }

    [Fact]
    public async Task Listing_before_anything_is_archived_is_empty()
    {
        Assert.Empty(await NewArchive().ListAsync(7));
    }

    [Fact]
    public async Task Fresh_link_for_an_older_export_works()
    {
        var archive = NewArchive();
        var saved = await archive.SaveAsync(7, March, April, "x,y\n", rowCount: 3);
        _clock.Advance(TimeSpan.FromDays(1));

        var again = await archive.GetLinkAsync(7, saved.Export.Name);

        Assert.Equal(3, again.Export.RowCount);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(15), again.LinkExpiresAt);
        using var response = await Http.GetAsync(again.DownloadUrl);
        Assert.Equal("x,y\n", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("company-8/hours_2030-03-01_to_2030-04-01_x.csv")] // another company's folder
    [InlineData("company-7/../company-8/hours.csv")]               // walking out of our folder
    [InlineData("company-7/does-not-exist.csv")]                    // just missing
    public async Task Links_for_other_companies_or_missing_blobs_are_not_found(string name)
    {
        var archive = NewArchive();
        await archive.SaveAsync(8, March, April, "theirs\n", rowCount: 1);

        var ex = await Assert.ThrowsAsync<AppException>(() => archive.GetLinkAsync(7, name));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void Archive_refuses_to_start_without_a_connection_string()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PayrollArchive(Options.Create(new PayrollArchiveOptions()), _clock));
    }
}
