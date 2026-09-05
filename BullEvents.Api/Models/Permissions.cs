namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Primitives
 * ------------------------------------------------------------------ */

/// <summary>
/// What can be done to an object.
///
/// A flags enum rather than a row per verb: a permission set holds one integer
/// per object instead of six rows, and "does this user have edit" is a bit test
/// rather than a lookup. The two wide grants are separate bits because they are
/// genuinely different powers — ViewAll defeats record sharing, ModifyAll
/// defeats sharing *and* ownership.
/// </summary>
[Flags]
public enum ObjectAction
{
    None = 0,
    View = 1 << 0,
    Create = 1 << 1,
    Edit = 1 << 2,
    Delete = 1 << 3,

    /// <summary>Reads every record of this object, ignoring the sharing rules.</summary>
    ViewAll = 1 << 4,

    /// <summary>Edits and deletes every record, ignoring sharing and ownership.</summary>
    ModifyAll = 1 << 5,

    Read = View,
    ReadWrite = View | Create | Edit,
    Full = View | Create | Edit | Delete,
    Everything = Full | ViewAll | ModifyAll,
}

/// <summary>
/// The objects permissions are expressed against.
///
/// Named constants rather than reflection over the DbSets: the permission
/// matrix is a stored, audited thing, and renaming a C# class should not
/// silently rewrite what somebody was granted.
/// </summary>
public static class SecuredObjects
{
    public const string Lead = "Lead";
    public const string Contact = "Contact";
    public const string Opportunity = "Opportunity";
    public const string Quotation = "Quotation";
    public const string Unit = "Unit";
    public const string Project = "Project";
    public const string SiteVisit = "SiteVisit";
    public const string ObmVisit = "ObmVisit";
    public const string FollowUp = "FollowUp";
    public const string Call = "Call";
    public const string Booking = "Booking";
    public const string Employee = "Employee";
    public const string Goal = "Goal";
    public const string Report = "Report";
    public const string User = "User";
    public const string Company = "Company";
    public const string Branch = "Branch";

    public static readonly string[] All =
    [
        Lead, Contact, Opportunity, Quotation, Unit, Project, SiteVisit,
        ObmVisit, FollowUp, Call, Booking, Employee, Goal, Report, User, Company, Branch,
    ];

    /// <summary>The objects a sales seat works day to day.</summary>
    public static readonly string[] SalesObjects =
        [Lead, Contact, Opportunity, Quotation, Unit, SiteVisit, ObmVisit, FollowUp, Call];
}

/// <summary>
/// The default visibility of an object before any sharing widens it —
/// Salesforce calls this the org-wide default.
///
/// It is the floor, not the ceiling: sharing rules, the role hierarchy and an
/// explicit ViewAll can each open a record up, but nothing narrows past this.
/// Set per company because two tenants on the same install genuinely disagree
/// about whether one rep should see another's pipeline.
/// </summary>
public enum ObjectVisibility
{
    /// <summary>Only the owner and those above them in the hierarchy.</summary>
    Private = 0,

    /// <summary>Everyone in the company reads it; only the owner edits.</summary>
    PublicRead = 1,

    /// <summary>Everyone in the company reads and edits it.</summary>
    PublicReadWrite = 2,
}

/* ------------------------------------------------------------------ *
 * Stored permission model
 * ------------------------------------------------------------------ */

/// <summary>
/// A named bundle of object permissions.
///
/// Two kinds share this table, told apart by <see cref="IsProfile"/>:
///
/// A <b>profile</b> is the baseline and every user has exactly one. It answers
/// "what does this job do" and is where the defaults live.
///
/// A <b>permission set</b> is additive and a user may hold any number. It
/// answers "what else does this particular person need" — the executive who
/// also approves discounts, the manager covering payroll for a month. Additive
/// because a grant that can also take things away makes the effective answer
/// depend on evaluation order, and nobody can predict it.
/// </summary>
public class PermissionSet : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>True for the one-per-user baseline, false for an add-on.</summary>
    public bool IsProfile { get; set; }

    /// <summary>
    /// The role this profile is the default for, when it is one. Lets a company
    /// tune what "Sales Executive" means without every user being re-pointed.
    /// </summary>
    public string? RoleKey { get; set; }

    /// <summary>Shipped with the product — renameable, not deletable.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Modules this bundle opens, over and above the role's own set.</summary>
    public string? ModulesCsv { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<ObjectPermission> ObjectPermissions { get; set; } = new List<ObjectPermission>();
    public ICollection<FieldPermission> FieldPermissions { get; set; } = new List<FieldPermission>();
    public ICollection<UserPermissionSet> Assignments { get; set; } = new List<UserPermissionSet>();
}

/// <summary>What one bundle grants on one object.</summary>
public class ObjectPermission
{
    public int Id { get; set; }
    public int PermissionSetId { get; set; }

    /// <summary>A key from <see cref="SecuredObjects"/>.</summary>
    public string Object { get; set; } = string.Empty;

    public ObjectAction Actions { get; set; }

    public PermissionSet? PermissionSet { get; set; }
}

/// <summary>
/// A field somebody may not see, or may see but not change.
///
/// Stored only for the exceptions. A field with no row here follows its
/// object's permission, so the table holds the handful of genuinely sensitive
/// columns — a lead's phone number for a seat that must not export it, the
/// discount on a quotation — rather than a row per field per profile.
/// </summary>
public class FieldPermission
{
    public int Id { get; set; }
    public int PermissionSetId { get; set; }

    public string Object { get; set; } = string.Empty;

    /// <summary>The DTO/field name as the API spells it, e.g. "phone".</summary>
    public string Field { get; set; } = string.Empty;

    public bool CanRead { get; set; } = true;
    public bool CanEdit { get; set; }

    public PermissionSet? PermissionSet { get; set; }
}

/// <summary>One additive bundle held by one user.</summary>
public class UserPermissionSet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PermissionSetId { get; set; }

    /// <summary>
    /// When the grant lapses. Cover for an absence is the common case, and a
    /// temporary permission that nobody remembers to remove is how access
    /// quietly accumulates.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public string? Reason { get; set; }
    public int? GrantedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public PermissionSet? PermissionSet { get; set; }
}

/// <summary>
/// A company's default visibility for one object.
///
/// Absent means <see cref="ObjectVisibility.Private"/> — the safe end. A
/// missing configuration row should never be the reason everybody can suddenly
/// read everything.
/// </summary>
public class ObjectVisibilityRule : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Object { get; set; } = string.Empty;
    public ObjectVisibility Visibility { get; set; } = ObjectVisibility.Private;

    /// <summary>
    /// Whether a manager inherits access to their reports' records.
    ///
    /// On for the sales objects, and deliberately switchable: HR records roll
    /// up a different tree than the sales one, and a company may want a
    /// reporting line that does not carry record access at all.
    /// </summary>
    public bool GrantAccessUsingHierarchy { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedById { get; set; }
}
