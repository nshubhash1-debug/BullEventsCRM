using System.ComponentModel.DataAnnotations;

namespace BullEvents.Api.Dtos;

public record CompanyDto(
    int Id, string Name, string Slug, string PlanTier, string Status, DateTime CreatedAt
);

/// <summary>
/// Provisions a whole tenant in one call: the company, its head-office branch
/// and the admin who will run it. Splitting these into three round trips left
/// half-built companies behind whenever the caller stopped after the first.
/// </summary>
public record CreateCompanyRequest(
    [Required, MinLength(2)] string Name,
    string? Slug,
    string? PlanTier,
    [Required, MinLength(2)] string HeadOfficeCity,
    string? HeadOfficeName,
    [Required, MinLength(2)] string AdminName,
    [Required, EmailAddress] string AdminEmail,
    [Required, MinLength(8)] string AdminPassword
);

public record UpdateCompanyRequest(
    [Required, MinLength(2)] string Name,
    string? PlanTier,
    string? Status
);

public record BranchDto(
    int Id, string Name, string City, string? Address, string? ContactPhone, int UserCount
);

public record CreateBranchRequest(
    [Required, MinLength(2)] string Name,
    [Required, MinLength(2)] string City,
    string? Address,
    string? ContactPhone
);

public record UpdateBranchRequest(
    [Required, MinLength(2)] string Name,
    [Required, MinLength(2)] string City,
    string? Address,
    string? ContactPhone
);

/// <summary>
/// One row of the user list.
///
/// Carries the access model alongside the identity — role, reporting line and
/// the branches a person can see — because the admin console's whole job is
/// answering "who can do what", and a list that shows only name and role sends
/// the administrator into a second screen for every row.
/// </summary>
public record UserListItemDto(
    int Id,
    string Name,
    string Email,
    string Role,
    /// <summary>The role's display name, resolved through the catalogue.</summary>
    string RoleName,
    /// <summary>How much data the role reaches: Own, Team, Company or Platform.</summary>
    string Scope,
    bool IsActive,
    IReadOnlyList<int> BranchIds,
    IReadOnlyList<string> BranchNames,
    int? ManagerId,
    string? ManagerName,
    /// <summary>How many people report to them, directly and below.</summary>
    int TeamSize,
    /// <summary>True while the account is still on the password it was created with.</summary>
    bool MustChangePassword,
    /// <summary>Null for an account that has never completed a sign-in.</summary>
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    /// <summary>Modules turned on or off for this person specifically.</summary>
    int ModuleOverrides
);

public record CreateUserRequest(
    [Required, MinLength(2)] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role,
    IReadOnlyList<int>? BranchIds,
    int? ManagerId,
    /// <summary>
    /// Whether the invitee must replace the password before anything else.
    /// Defaults on, because a password an administrator typed is a password two
    /// people know.
    /// </summary>
    bool? MustChangePassword
);

public record UpdateUserRequest(
    [Required] string Role,
    bool IsActive,
    IReadOnlyList<int>? BranchIds,
    int? ManagerId
);

/// <summary>
/// An administrator setting a new password on somebody else's account.
///
/// Deliberately separate from the self-service change: there is no current
/// password to prove here, so the action is restricted to administrators and
/// always leaves the account blocked until the owner replaces what was set.
/// </summary>
public record ResetUserPasswordRequest(
    [Required, MinLength(8)] string NewPassword
);

/// <summary>Activate or deactivate several accounts in one call.</summary>
public record BulkUserStatusRequest(
    [Required] IReadOnlyList<int> UserIds,
    bool IsActive
);
