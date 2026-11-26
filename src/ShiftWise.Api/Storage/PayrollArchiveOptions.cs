namespace ShiftWise.Api.Storage;

// Bound from the "PayrollArchive" section. When ConnectionString is empty the
// archive endpoints answer 503 and nothing talks to Azure.
public class PayrollArchiveOptions
{
    public string ConnectionString { get; set; } = "";

    public string ContainerName { get; set; } = "payroll-exports";

    // How long a download link handed back to a manager stays valid.
    public int DownloadLinkMinutes { get; set; } = 15;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
