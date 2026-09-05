namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Business hours
 * ------------------------------------------------------------------ */

/// <summary>
/// The working week a branch keeps, and the days it does not.
///
/// This is the foundation the SLA clock stands on. "Respond within four hours"
/// means four <em>working</em> hours — a lead that arrives at six on Friday
/// evening is not late by Saturday lunchtime, and a system that says otherwise
/// teaches everyone to ignore its alerts.
/// </summary>
public class BusinessHours : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Null when these are the company-wide hours.</summary>
    public int? BranchId { get; set; }

    /// <summary>
    /// IANA zone the times are read in — "Asia/Kolkata".
    ///
    /// Stored rather than assumed: a developer with a Dubai desk keeps two sets
    /// of hours, and an SLA computed in the server's timezone would be wrong for
    /// one of them all year.
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    /// <summary>Minutes past midnight, per day, indexed from Sunday. -1 means closed.</summary>
    public int SundayOpen { get; set; } = -1;
    public int SundayClose { get; set; } = -1;
    public int MondayOpen { get; set; } = 600;
    public int MondayClose { get; set; } = 1140;
    public int TuesdayOpen { get; set; } = 600;
    public int TuesdayClose { get; set; } = 1140;
    public int WednesdayOpen { get; set; } = 600;
    public int WednesdayClose { get; set; } = 1140;
    public int ThursdayOpen { get; set; } = 600;
    public int ThursdayClose { get; set; } = 1140;
    public int FridayOpen { get; set; } = 600;
    public int FridayClose { get; set; } = 1140;
    public int SaturdayOpen { get; set; } = 600;
    public int SaturdayClose { get; set; } = 1140;

    /// <summary>The one set used when nothing more specific applies.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public List<Holiday> Holidays { get; set; } = [];
}

/// <summary>A day the office is shut, and the SLA clock stops.</summary>
public class Holiday : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessHoursId { get; set; }

    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Repeats on the same date every year — Republic Day, not Diwali.</summary>
    public bool IsRecurring { get; set; }

    public BusinessHours? BusinessHours { get; set; }
}

/* ------------------------------------------------------------------ *
 * Assignment rules
 * ------------------------------------------------------------------ */

public static class AssignmentStrategies
{
    /// <summary>Everyone in the pool in turn.</summary>
    public const string RoundRobin = "RoundRobin";

    /// <summary>Whoever in the pool currently holds the fewest open records.</summary>
    public const string LeastLoaded = "LeastLoaded";

    /// <summary>One named person, every time.</summary>
    public const string Fixed = "Fixed";

    public static readonly string[] All = [RoundRobin, LeastLoaded, Fixed];
}

/// <summary>
/// Who a new lead goes to.
///
/// Rules are tried in <see cref="SortOrder"/> and the first match wins — the
/// same "first match wins" the industry has trained everyone on, and the reason
/// the screen shows the order rather than hiding it. A rule with no criteria is
/// the catch-all and belongs last.
/// </summary>
public class AssignmentRule : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>A key from <see cref="SecuredObjects"/>. Leads, in practice.</summary>
    public string Object { get; set; } = SecuredObjects.Lead;

    public int SortOrder { get; set; }

    /* ---------------- when it fires ---------------- */

    public string? CriteriaField { get; set; }
    public string? CriteriaOperator { get; set; }
    public string? CriteriaValue { get; set; }

    /* ---------------- who gets it ---------------- */

    public string Strategy { get; set; } = AssignmentStrategies.RoundRobin;

    /// <summary>Comma-separated user ids the strategy picks from.</summary>
    public string? PoolUserIds { get; set; }

    /// <summary>Everyone holding this role joins the pool.</summary>
    public string? PoolRoleKey { get; set; }

    public int? FixedUserId { get; set; }

    /// <summary>
    /// Where round-robin got to, so the next lead goes to the next person
    /// rather than restarting at the top of the list every time the process
    /// recycles.
    /// </summary>
    public int RoundRobinCursor { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ------------------------------------------------------------------ *
 * Duplicate rules
 * ------------------------------------------------------------------ */

public static class DuplicateActions
{
    /// <summary>Save it, but tell the person what it looks like.</summary>
    public const string Warn = "Warn";

    /// <summary>Refuse the save outright.</summary>
    public const string Block = "Block";

    public static readonly string[] All = [Warn, Block];
}

/// <summary>
/// What counts as the same person arriving twice.
///
/// Matched on the fields a duplicate actually shares — a phone number, an email
/// — rather than on a fuzzy score over the name. Two brothers at one address
/// are not a duplicate, and a rule that says they are gets switched off within
/// the week.
/// </summary>
public class DuplicateRule : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Object { get; set; } = SecuredObjects.Lead;

    /// <summary>Comma-separated field names that must all match.</summary>
    public string MatchFields { get; set; } = "Phone";

    public string Action { get; set; } = DuplicateActions.Warn;

    /// <summary>
    /// Whether a match against a record somebody else owns still counts.
    ///
    /// Usually yes: the whole point is to stop two reps working the same person
    /// without knowing it.
    /// </summary>
    public bool AcrossOwners { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ------------------------------------------------------------------ *
 * Escalation and SLA
 * ------------------------------------------------------------------ */

public static class EscalationActions
{
    public const string NotifyOwner = "NotifyOwner";
    public const string NotifyManager = "NotifyManager";
    public const string Reassign = "Reassign";
    public const string RaisePriority = "RaisePriority";

    public static readonly string[] All = [NotifyOwner, NotifyManager, Reassign, RaisePriority];
}

/// <summary>
/// What happens when a record sits too long.
///
/// The clock runs in working hours from <see cref="BusinessHours"/>, so a
/// four-hour target does not expire over a weekend. Steps fire in order and
/// each one only once — an escalation that re-fires every sweep is an
/// escalation everyone mutes.
/// </summary>
public class EscalationRule : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Object { get; set; } = SecuredObjects.Lead;

    /// <summary>Which records the clock runs on. Blank means all of them.</summary>
    public string? CriteriaField { get; set; }
    public string? CriteriaOperator { get; set; }
    public string? CriteriaValue { get; set; }

    /// <summary>
    /// What the clock measures from: <c>Created</c>, <c>LastActivity</c> or
    /// <c>Assigned</c>.
    /// </summary>
    public string StartsFrom { get; set; } = "Created";

    /// <summary>Working minutes before the target is missed.</summary>
    public int TargetMinutes { get; set; } = 240;

    public int? BusinessHoursId { get; set; }

    public string Action { get; set; } = EscalationActions.NotifyManager;

    /// <summary>Set when the action is <see cref="EscalationActions.Reassign"/>.</summary>
    public int? ReassignToUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public BusinessHours? BusinessHours { get; set; }
}

/// <summary>One record that breached a rule, kept so the same alert fires once.</summary>
public class EscalationEvent : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EscalationRuleId { get; set; }
    public string Object { get; set; } = string.Empty;
    public int RecordId { get; set; }

    public DateTime BreachedAt { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string? Outcome { get; set; }

    public EscalationRule? EscalationRule { get; set; }
}

/* ------------------------------------------------------------------ *
 * Approvals
 * ------------------------------------------------------------------ */

public static class ApproverKinds
{
    /// <summary>The submitter's manager, whoever that is on the day.</summary>
    public const string Manager = "Manager";

    /// <summary>Anyone holding a named role.</summary>
    public const string Role = "Role";

    /// <summary>One named person.</summary>
    public const string User = "User";

    public static readonly string[] All = [Manager, Role, User];
}

// The statuses a decision can be in already exist on the quotation side, in
// Pricing.cs, and they are the same four words here. Reused rather than
// redeclared: two enums that agree today are two enums that disagree later.

/// <summary>
/// A sequence of sign-offs a record has to clear.
///
/// Modelled as ordered steps rather than a free-form graph on purpose: an
/// approval that can branch is one nobody can answer "who has it now" about,
/// and that question is the only one anybody ever asks.
/// </summary>
public class ApprovalProcess : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Object { get; set; } = SecuredObjects.Quotation;

    /// <summary>Which records need it. Blank means every one of them.</summary>
    public string? CriteriaField { get; set; }
    public string? CriteriaOperator { get; set; }
    public string? CriteriaValue { get; set; }

    /// <summary>Locked records cannot be edited while a decision is outstanding.</summary>
    public bool LockRecord { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public List<ApprovalStep> Steps { get; set; } = [];
}

public class ApprovalStep : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ApprovalProcessId { get; set; }

    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;

    public string ApproverKind { get; set; } = ApproverKinds.Manager;
    public string? ApproverRoleKey { get; set; }
    public int? ApproverUserId { get; set; }

    public ApprovalProcess? ApprovalProcess { get; set; }
}

/* ------------------------------------------------------------------ *
 * Scheduled jobs
 * ------------------------------------------------------------------ */

public static class JobKinds
{
    public const string EscalationSweep = "EscalationSweep";
    public const string InterestAccrual = "InterestAccrual";
    public const string UsageSnapshot = "UsageSnapshot";
    public const string DemandReminder = "DemandReminder";
    public const string DataExport = "DataExport";

    public static readonly string[] All =
        [EscalationSweep, InterestAccrual, UsageSnapshot, DemandReminder, DataExport];
}

/// <summary>
/// A recurring job, its schedule, and what happened last time it ran.
///
/// The last run is stored on the row rather than only in a log because the
/// question an administrator opens this screen with is "did it run", and an
/// answer that requires reading a log file is not an answer.
/// </summary>
public class ScheduledJob : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;

    /// <summary>Standard five-field cron. Read in the company's timezone.</summary>
    public string Cron { get; set; } = "0 * * * *";

    public bool IsActive { get; set; } = true;

    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    /// <summary>Ok, Failed, or null before the first run.</summary>
    public string? LastOutcome { get; set; }
    public string? LastMessage { get; set; }
    public int LastDurationMs { get; set; }
    public int RunCount { get; set; }
    public int FailureCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}
