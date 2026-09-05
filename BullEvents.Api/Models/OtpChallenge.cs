namespace BullEvents.Api.Models;

/// <summary>
/// One outstanding sign-in code.
///
/// The code is stored hashed, never in clear, for the same reason a password
/// is: a database copy must not hand somebody a working second factor. It is
/// short-lived and single-use, and it counts its own failed attempts so a
/// six-digit code cannot be walked through a million guesses — which, without
/// a limit, takes a script about a minute.
/// </summary>
public class OtpChallenge
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>BCrypt of the code as sent. The clear code exists only in the email.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Wrong guesses so far. At the ceiling the challenge is dead.</summary>
    public int Attempts { get; set; }

    /// <summary>Set the moment it is spent, so the same code cannot be replayed.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>Where it went, masked, for the sign-in trail.</summary>
    public string? SentTo { get; set; }

    public User? User { get; set; }
}
