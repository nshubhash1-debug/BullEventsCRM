namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Designations
 * ------------------------------------------------------------------ */

/// <summary>
/// A job title, as this company writes it.
///
/// Deliberately not the same thing as <see cref="Roles"/>. A role is what the
/// software lets you do and there are ten of them, fixed, because every one is
/// wired into permission checks. A designation is what the business calls you
/// on a visiting card — "Senior Relationship Manager", "AVP – Channel Sales" —
/// and every developer has their own list, changes it, and expects to add one
/// without a deployment.
///
/// Conflating the two is the usual mistake, and it ends with either a
/// permission model nobody can reason about or an org chart nobody can print.
/// </summary>
public class Designation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short form for lists and org charts — "SRM", "AVP-CS".</summary>
    public string? Code { get; set; }

    /// <summary>
    /// Seniority, 1 being the top.
    ///
    /// A number rather than an ordering of rows, so two designations can sit at
    /// the same grade — a company usually has several titles that are peers,
    /// and forcing them into a strict order invents a hierarchy that does not
    /// exist.
    /// </summary>
    public int Level { get; set; } = 5;

    public string? Description { get; set; }

    /// <summary>
    /// The role usually given to somebody with this title.
    ///
    /// A suggestion the user form pre-fills, not a rule. It saves the common
    /// case without pretending that a title determines access — the same
    /// "Manager" title covers a sales manager and a post-sales manager, who see
    /// entirely different things.
    /// </summary>
    public string? SuggestedRole { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ------------------------------------------------------------------ *
 * Teams
 * ------------------------------------------------------------------ */

public static class TeamKinds
{
    public const string Sales = "Sales";
    public const string TeleSales = "TeleSales";
    public const string ChannelPartner = "ChannelPartner";
    public const string PostSales = "PostSales";
    public const string Collections = "Collections";
    public const string CustomerCare = "CustomerCare";
    public const string Marketing = "Marketing";
    public const string Construction = "Construction";
    public const string BackOffice = "BackOffice";

    public static readonly string[] All =
    [
        Sales, TeleSales, ChannelPartner, PostSales, Collections,
        CustomerCare, Marketing, Construction, BackOffice,
    ];

    public static string Label(string kind) => kind switch
    {
        TeleSales => "Tele-sales",
        ChannelPartner => "Channel partner",
        PostSales => "Post-sales",
        CustomerCare => "Customer care",
        BackOffice => "Back office",
        _ => kind,
    };
}

/// <summary>
/// A named group of people who work the same desk.
///
/// Distinct from the reporting line on <see cref="User.ManagerId"/>, which
/// answers "who does this person report to" and drives record visibility. A
/// team answers "who works together" — the same person can report to one
/// manager and sit on two teams, and a target set on a team is not the same as
/// a target rolled up a management chain.
///
/// Held per branch where it belongs to one, because most developers run a
/// sales team per site rather than one national desk.
/// </summary>
public class Team : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public string Kind { get; set; } = TeamKinds.Sales;

    /// <summary>Null for a team that spans branches — a central collections desk.</summary>
    public int? BranchId { get; set; }

    /// <summary>
    /// Who runs it.
    ///
    /// Kept on the team rather than inferred from a member's role, because the
    /// person leading a team is a fact somebody decided, and inferring it from
    /// whoever happens to hold the most senior title picks the wrong person the
    /// moment two peers sit on the same desk.
    /// </summary>
    public int? LeadUserId { get; set; }

    /// <summary>A team of teams — a region holding its site desks.</summary>
    public int? ParentTeamId { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Branch? Branch { get; set; }
    public User? LeadUser { get; set; }
    public Team? ParentTeam { get; set; }
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}

public static class TeamMemberRoles
{
    public const string Member = "Member";
    public const string Lead = "Lead";

    /// <summary>Sits on the team but reports elsewhere — a shared specialist.</summary>
    public const string Associate = "Associate";

    public static readonly string[] All = [Member, Lead, Associate];
}

/// <summary>
/// One person's membership of one team.
///
/// Closed with a date rather than deleted. "Who was on this team in
/// September" is a question every incentive dispute turns on, and a row that
/// vanished when somebody moved desks cannot answer it — the commission was
/// earned by whoever held the desk at the time, not by whoever holds it now.
/// </summary>
public class TeamMember : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int TeamId { get; set; }
    public int UserId { get; set; }

    public string RoleInTeam { get; set; } = TeamMemberRoles.Member;

    public DateTime JoinedOn { get; set; } = DateTime.UtcNow.Date;

    /// <summary>Null while they are still on it. Set, never deleted, when they leave.</summary>
    public DateTime? LeftOn { get; set; }

    /// <summary>
    /// Their home team, when they sit on more than one.
    ///
    /// Exactly one live membership per person carries this, so a report that
    /// counts headcount by team does not count the same person twice.
    /// </summary>
    public bool IsPrimary { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team? Team { get; set; }
    public User? User { get; set; }

    public bool IsLive => LeftOn is null;
}

/// <summary>
/// A move between teams, recorded as its own event.
///
/// The two memberships either side already carry their dates, so this is
/// strictly redundant — and worth keeping anyway. A transfer has a reason and
/// an author, and reconstructing "why did six people leave the Andheri desk in
/// March" from a pile of closed memberships is exactly the kind of question
/// that gets asked once and answered badly.
///
/// A null <see cref="FromTeamId"/> is somebody joining their first team; a null
/// <see cref="ToTeamId"/> is somebody leaving without a destination.
/// </summary>
public class TeamTransfer : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int UserId { get; set; }

    public int? FromTeamId { get; set; }
    public int? ToTeamId { get; set; }

    public DateTime EffectiveOn { get; set; } = DateTime.UtcNow.Date;
    public string? Reason { get; set; }

    public int? MovedByUserId { get; set; }
    public string? MovedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Team? FromTeam { get; set; }
    public Team? ToTeam { get; set; }
}
