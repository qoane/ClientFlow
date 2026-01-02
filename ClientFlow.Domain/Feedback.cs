using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClientFlow.Domain.Feedback;

/// <summary>
/// Represents an employee or consultant who can be selected in the kiosk.
/// </summary>
public class Staff
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? PhotoUrl { get; set; }
    /// <summary>
    /// When true this staff member will be returned in the active roster for the kiosk.
    /// Admin users can toggle this flag via the staff management API.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The branch that this staff member belongs to.  When null the staff
    /// member is considered global and may be visible in all branches.  Most
    /// staff members should be assigned to a specific branch so that
    /// administrators can manage their own roster independently.
    /// </summary>
    public Guid? BranchId { get; set; }
    public ClientFlow.Domain.Branches.Branch? Branch { get; set; }

    public ICollection<KioskFeedback> Feedback { get; set; } = new List<KioskFeedback>();
}

/// <summary>
/// A record of a single kiosk session.  Each feedback entry contains the
/// phone number, the staff member who assisted, and the three numeric
/// ratings supplied by the user.  Additional metadata such as
/// timestamps are captured for reporting.
/// </summary>
public class KioskFeedback
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    [MaxLength(64)]
    public string? Phone { get; set; }
    public Guid StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int TimeRating { get; set; }
    public int RespectRating { get; set; }
    public int OverallRating { get; set; }
    public int? RecommendRating { get; set; }

    [MaxLength(256)]
    public string? ServiceType { get; set; }
    [MaxLength(32)]
    public string? Gender { get; set; }
    [MaxLength(32)]
    public string? AgeRange { get; set; }
    [MaxLength(64)]
    public string? City { get; set; }
    public string? PoliciesJson { get; set; }
    [MaxLength(16)]
    public string? ContactPreference { get; set; }
    public string? Comment { get; set; }

    /// <summary>
    /// UTC timestamp indicating when the kiosk session was started.  This
    /// allows calculation of session duration and reporting by time of
    /// day.  Defaults to the creation timestamp when not supplied.
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The total time in seconds that the client took to complete the
    /// survey.  Calculated by the kiosk client as the difference
    /// between when the phone page is first presented and when the
    /// thank you page is shown.  Optional; may be 0 if not provided.
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// The branch from which this feedback was collected.  This ties the
    /// feedback to a physical location.  Kiosk clients must provide
    /// the branch identifier when submitting feedback.
    /// </summary>
    public Guid BranchId { get; set; }
    public ClientFlow.Domain.Branches.Branch Branch { get; set; } = null!;

    /// <summary>
    /// A human‑readable name of the branch.  This property is retained for backward
    /// compatibility with existing APIs that reference the Branch by name rather than
    /// identifier.  Going forward the BranchId should be used for relational lookups.
    /// </summary>
    public string? BranchName { get; set; }
}
