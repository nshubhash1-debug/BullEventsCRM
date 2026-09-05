using System.ComponentModel.DataAnnotations;

namespace BullEvents.Api.Dtos;

/// <summary>
/// Sign-in credentials.
///
/// No length rule on the password on purpose. A sign-in is a comparison against
/// a stored hash — it either matches or it does not — and a minimum here only
/// publishes the policy to anyone probing the endpoint while locking out any
/// account whose password predates the current rule. Length is enforced where
/// a password is <em>chosen</em>: see <see cref="ChangePasswordRequest"/>.
/// </summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

/// <summary>
/// <paramref name="Delivered"/> is false when no mail provider is connected, so
/// the sign-in screen can say the code was not actually sent instead of telling
/// the user to check an inbox nothing reached.
/// </summary>
public record LoginResponse(
    string PendingToken,
    string MaskedContact,
    bool Delivered,
    /// <summary>
    /// Filled in only when the second factor is switched off, which is possible
    /// in Development alone. When present the client is already signed in and
    /// must not show the code screen; when null the normal two-step flow runs.
    /// </summary>
    AuthResponse? Session = null);

public record VerifyOtpRequest(
    [Required] string PendingToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code
);

/// <summary>
/// One tenant the signed-in user is allowed to open. A platform admin gets the
/// whole list; everyone else gets exactly their own company, so the client can
/// use one code path for both.
/// </summary>
public record CompanyOptionDto(
    int Id,
    string Name,
    string Slug,
    string PlanTier,
    string Status,
    int BranchCount,
    int UserCount,
    bool IsHome
);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserProfileDto User,
    // True when the client must call select-company before the CRM can load.
    bool RequiresCompanySelection,
    IReadOnlyList<CompanyOptionDto> Companies,
    // True while the account still carries the password it was created with.
    bool MustChangePassword
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(10)] string NewPassword
);

/// <summary>Re-issues the access token against a different tenant.</summary>
public record SelectCompanyRequest([Required] int CompanyId);

public record UserProfileDto(
    int Id,
    string Name,
    string Email,
    string Role,
    int CompanyId,
    string CompanyName,
    IReadOnlyList<int> BranchIds
);

/// <summary>One device somebody is signed in on.</summary>
public record SessionDto(
    int Id,
    string Device,
    string? IpAddress,
    DateTime IssuedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    /// <summary>True for the session making this request — the one not to sign out by accident.</summary>
    bool IsCurrent
);
