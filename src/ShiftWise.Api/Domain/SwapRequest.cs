namespace ShiftWise.Api.Domain;

public enum SwapStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

// An employee asks to hand one of their shifts to a coworker.
// A manager has to approve it before the shift actually moves.
public class SwapRequest
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public int RequestedById { get; set; }
    public User? RequestedBy { get; set; }

    public int TargetEmployeeId { get; set; }
    public User? TargetEmployee { get; set; }

    public SwapStatus Status { get; set; } = SwapStatus.Pending;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedById { get; set; }
}
