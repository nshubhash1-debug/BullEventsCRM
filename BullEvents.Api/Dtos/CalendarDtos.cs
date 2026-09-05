namespace BullEvents.Api.Dtos;

/// <summary>
/// One thing on the calendar, whatever object it came from.
///
/// Site visits, field meetings and follow-ups are three different tables with
/// three different date fields, but on a calendar they are all just "something
/// at a time". Flattening them here means the grid renders one shape and does
/// not have to know which table a block came from — while <see cref="Source"/>
/// and <see cref="Url"/> keep the route back to the real record.
/// </summary>
public record CalendarEventDto(
    /// <summary>Unique across sources, e.g. "siteVisit:412".</summary>
    string Id,
    /// <summary>siteVisit, obmVisit or followUp.</summary>
    string Source,
    int SourceId,
    string Title,
    string? Subtitle,
    DateTime Start,
    DateTime End,
    /// <summary>True when the record carries no real duration, e.g. a task due date.</summary>
    bool IsPoint,
    string Status,
    /// <summary>Channel for follow-ups, visit type for visits — the second-level kind.</summary>
    string? Kind,
    string? Priority,
    int? OwnerId,
    string? OwnerName,
    int? BranchId,
    string? BranchName,
    /// <summary>Where the record lives in the app.</summary>
    string Url,
    string? Location,
    /// <summary>True once the block is over and it never reached a closed status.</summary>
    bool IsOverdue
);

/// <summary>Per-source counts for the calendar's filter chips.</summary>
public record CalendarSummaryDto(
    int Total,
    int SiteVisits,
    int ObmVisits,
    int FollowUps,
    int Overdue,
    int Completed
);

public record CalendarResponse(
    DateTime From,
    DateTime To,
    IReadOnlyList<CalendarEventDto> Events,
    CalendarSummaryDto Summary
);
