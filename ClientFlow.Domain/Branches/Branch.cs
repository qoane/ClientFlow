namespace ClientFlow.Domain.Branches;

/// <summary>
/// Represents a physical kiosk location or branch.  Each branch can have its
/// own report time and recipient list.  Staff and kiosk feedback are
/// associated with a branch via BranchId.
/// </summary>
public class Branch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Comma‑separated list of email addresses that should receive the daily
    /// report for this branch.  May be empty if no reports should be sent.
    /// </summary>
    public string? ReportRecipients { get; set; }
    /// <summary>
    /// Time of day to send the daily report (HH:mm).  If null or empty,
    /// the default time of 08:00 will be used.
    /// </summary>
    public string? ReportTime { get; set; }
}