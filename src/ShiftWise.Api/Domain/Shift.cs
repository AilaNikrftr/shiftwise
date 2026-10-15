namespace ShiftWise.Api.Domain;

public class Shift
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    // Stored as the business's local time. Time zones are out of scope for now.
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public string Position { get; set; } = "";
    public string? Notes { get; set; }
}
