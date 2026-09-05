using System.Security.Cryptography;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Issues and checks the sign-in code.
///
/// What makes this a real second factor rather than a formality: the code is
/// random per attempt, hashed at rest, expires in minutes, works once, and dies
/// after a handful of wrong guesses. Drop any one of those and six digits stop
/// being a meaningful obstacle.
/// </summary>
public class OtpService(AppDbContext db, IEmailSender email, ILogger<OtpService> logger)
{
    /// <summary>Long enough to fetch an email, short enough that a leaked code goes stale.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Wrong guesses allowed. Six digits is a million combinations, which a
    /// script exhausts in about a minute — the limit, not the length, is what
    /// makes the code hard to guess.
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>
    /// Generates a code, stores its hash, and emails the clear copy.
    ///
    /// Any earlier outstanding challenge for the user is spent first, so a
    /// second sign-in attempt invalidates the first code rather than leaving
    /// two valid at once.
    /// </summary>
    public async Task IssueAsync(User user, CancellationToken ct = default)
    {
        await db.OtpChallenges
            .Where(c => c.UserId == user.Id && c.ConsumedAt == null)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ConsumedAt, DateTime.UtcNow), ct);

        // RandomNumberGenerator, not Random: the latter is seeded predictably and
        // its output can be reconstructed from a couple of observed codes.
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        db.OtpChallenges.Add(new OtpChallenge
        {
            UserId = user.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            ExpiresAt = DateTime.UtcNow.Add(Lifetime),
            SentTo = MaskEmail(user.Email),
        });

        await db.SaveChangesAsync(ct);

        await email.SendAsync(
            user.Email,
            "Your Bull Realty CRM sign-in code",
            $"""
             {code} is your sign-in code.

             It expires in {Lifetime.TotalMinutes:0} minutes and can be used once.

             If you did not try to sign in, someone else has your password.
             Change it as soon as you can.
             """,
            ct);

        logger.LogInformation("Sign-in code issued for user {UserId}.", user.Id);
    }

    /// <summary>Why a check failed, in the caller's terms.</summary>
    public enum Result
    {
        Ok,
        NoChallenge,
        Expired,
        TooManyAttempts,
        Wrong,
    }

    public async Task<Result> VerifyAsync(int userId, string code, CancellationToken ct = default)
    {
        var challenge = await db.OtpChallenges
            .Where(c => c.UserId == userId && c.ConsumedAt == null)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (challenge is null) return Result.NoChallenge;

        if (challenge.ExpiresAt < DateTime.UtcNow)
        {
            challenge.ConsumedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result.Expired;
        }

        if (challenge.Attempts >= MaxAttempts)
        {
            challenge.ConsumedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result.TooManyAttempts;
        }

        if (!BCrypt.Net.BCrypt.Verify(code, challenge.CodeHash))
        {
            challenge.Attempts += 1;

            // The last wrong guess closes the challenge rather than leaving it
            // sitting at the limit for the next request to trip over.
            if (challenge.Attempts >= MaxAttempts) challenge.ConsumedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return challenge.Attempts >= MaxAttempts ? Result.TooManyAttempts : Result.Wrong;
        }

        challenge.ConsumedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok;
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return "***";

        return string.Concat(email.AsSpan(0, Math.Min(2, at)), "***", email.AsSpan(at));
    }
}
