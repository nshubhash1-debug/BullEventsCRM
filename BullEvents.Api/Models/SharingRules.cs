namespace BullEvents.Api.Models;

/// <summary>
/// Who a sharing rule opens records up to.
/// </summary>
public enum ShareTarget
{
    /// <summary>Everyone holding a role.</summary>
    Role = 0,

    /// <summary>Everyone holding a role, plus everyone reporting into them.</summary>
    RoleAndSubordinates = 1,

    /// <summary>Everyone assigned to a branch.</summary>
    Branch = 2,

    /// <summary>One named person.</summary>
    User = 3,
}

/// <summary>
/// A standing rule that widens who can see a slice of records.
///
/// Sharing only ever adds. There is no rule that takes access away, because the
/// moment both directions exist the answer depends on which rule ran last, and
/// an access model nobody can predict is one nobody can audit. Narrowing is
/// done by lowering the object's visibility, in one place, on purpose.
///
/// Two flavours, told apart by whether <see cref="OwnerRoleKey"/> is set:
/// owner-based ("everything owned by Tele Sales goes to the Tele Sales AGM")
/// and criteria-based ("every lead in Varanasi goes to the Varanasi branch").
/// </summary>
public class SharingRule : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>A key from <see cref="SecuredObjects"/>.</summary>
    public string Object { get; set; } = string.Empty;

    /* ---------------- which records ---------------- */

    /// <summary>Owner-based: records owned by anyone holding this role.</summary>
    public string? OwnerRoleKey { get; set; }

    /// <summary>Criteria-based: the field to test, as the query engine spells it.</summary>
    public string? CriteriaField { get; set; }
    public string? CriteriaOperator { get; set; }
    public string? CriteriaValue { get; set; }

    /* ---------------- to whom ---------------- */

    public ShareTarget Target { get; set; }

    /// <summary>Set for the role targets.</summary>
    public string? TargetRoleKey { get; set; }

    /// <summary>Set for the branch target.</summary>
    public int? TargetBranchId { get; set; }

    /// <summary>Set for the single-user target.</summary>
    public int? TargetUserId { get; set; }

    /// <summary>Read alone, or read and write.</summary>
    public bool GrantEdit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// When and from where an account may sign in.
///
/// Held per profile rather than per user so a policy is a property of the job:
/// "Tele Sales works ten to seven from the office" is one rule, not forty
/// copies that drift apart the first time somebody is hired.
/// </summary>
public class LoginPolicy : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int PermissionSetId { get; set; }

    /// <summary>
    /// Comma-separated CIDR ranges sign-in is allowed from. Empty means
    /// anywhere — the honest default for a field sales team on mobile data.
    /// </summary>
    public string? AllowedIpRanges { get; set; }

    /// <summary>Local-time window, in minutes past midnight. Null means any hour.</summary>
    public int? LoginFromMinute { get; set; }
    public int? LoginToMinute { get; set; }

    /// <summary>Days of the week sign-in is allowed, as a bitmask from Sunday.</summary>
    public int AllowedDays { get; set; } = 0b1111111;

    /// <summary>Minutes of inactivity before the session is dropped. Null uses the token's own life.</summary>
    public int? IdleTimeoutMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public PermissionSet? PermissionSet { get; set; }
}
