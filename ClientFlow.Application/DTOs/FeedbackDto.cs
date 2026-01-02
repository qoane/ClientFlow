namespace ClientFlow.Application.DTOs;

/// <summary>
/// Data transfer object for receiving kiosk feedback submissions.  A client should provide
/// either a StaffId or StaffName to identify the staff member who assisted.  Ratings are
/// integers between 1 and 5.  BranchId is the preferred way to indicate which branch the
/// feedback belongs to; BranchName is provided for backward compatibility with older
/// clients that may still send a branch name.  If both BranchId and BranchName are null
/// the feedback is considered unassigned.
/// </summary>
public sealed record FeedbackDto(
    Guid? StaffId,
    string? StaffName,
    int? TimeRating,
    int? RespectRating,
    int? OverallRating,
    int? RecommendRating,
    string? Phone,
    string? ServiceType,
    string? Gender,
    string? AgeRange,
    string? City,
    IReadOnlyList<string>? Policies,
    string? ContactPreference,
    string? Comment,
    DateTimeOffset? StartedUtc,
    int? DurationSeconds,
    Guid? BranchId,
    string? BranchName
);
