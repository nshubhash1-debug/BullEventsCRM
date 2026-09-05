namespace BullEvents.Api.Models;

/// <summary>
/// One module explicitly granted to, or taken from, a single user.
///
/// Stored as a signed decision rather than as "the list of modules this user
/// has", so the role stays the source of truth and the override says only where
/// this person differs from it. When the role's module set later changes,
/// everyone moves with it except where somebody deliberately said otherwise.
/// </summary>
public class UserModuleGrant
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>A key from <see cref="Modules"/>.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>True adds the module, false removes one the role would grant.</summary>
    public bool Granted { get; set; }

    /// <summary>Why the exception exists — the question asked at the next audit.</summary>
    public string? Reason { get; set; }

    public int? GrantedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
