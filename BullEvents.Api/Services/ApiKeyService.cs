using System.Security.Cryptography;
using System.Text;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>Mints, verifies and retires the credentials integrations use.</summary>
public class ApiKeyService(AppDbContext db)
{
    /// <summary>
    /// Prefixed so a leaked key is recognisable on sight.
    ///
    /// Secret scanners match on patterns like this, and so does a human reading
    /// a pull request. A bare random string in a config file looks like every
    /// other bare random string until somebody uses it.
    /// </summary>
    private const string Prefix = "brg_live_";

    /// <summary>How much of the key is kept in clear, including the prefix.</summary>
    private const int PrefixLength = 16;

    public record Minted(ApiKey Key, string Secret);

    /// <summary>
    /// Creates a key and returns the only copy of its secret that will ever
    /// exist. The caller shows it once; after that the hash is all there is.
    /// </summary>
    public async Task<Minted> CreateAsync(
        int companyId,
        string name,
        IReadOnlyList<string> scopes,
        DateTime? expiresAt,
        int? createdById,
        string? createdByName,
        CancellationToken ct = default)
    {
        var secret = Prefix + Base62(32);

        var key = new ApiKey
        {
            CompanyId = companyId,
            Name = name.Trim(),
            Prefix = secret[..PrefixLength],
            SecretHash = Hash(secret),
            ScopesCsv = string.Join(',', scopes.Where(ApiScopes.Exists)),
            ExpiresAt = expiresAt,
            CreatedById = createdById,
            CreatedByName = createdByName,
        };

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);

        return new Minted(key, secret);
    }

    /// <summary>
    /// Resolves a presented key, or null.
    ///
    /// Filters are ignored because this runs before the request has a tenant —
    /// the key is what decides which tenant it is.
    /// </summary>
    public async Task<ApiKey?> VerifyAsync(string presented, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presented) || presented.Length < PrefixLength) return null;

        var prefix = presented[..PrefixLength];

        var candidates = await db.ApiKeys
            .IgnoreQueryFilters()
            .Where(k => k.Prefix == prefix && k.RevokedAt == null)
            .ToListAsync(ct);

        var hash = Hash(presented);
        var now = DateTime.UtcNow;

        foreach (var candidate in candidates)
        {
            // Constant time: comparing hashes with == leaks how many characters
            // matched through timing, which is enough to walk a hash out one
            // character at a time.
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(candidate.SecretHash),
                    Encoding.UTF8.GetBytes(hash)))
            {
                continue;
            }

            if (!candidate.IsLive(now)) return null;

            candidate.LastUsedAt = now;
            candidate.LastUsedIp = ip;
            candidate.CallCount++;
            await db.SaveChangesAsync(ct);

            return candidate;
        }

        return null;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Alphanumeric only. A key travels through URLs, YAML files and shell
    /// arguments, and every symbol is one more place it gets mangled.
    /// </summary>
    private static string Base62(int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        var bytes = RandomNumberGenerator.GetBytes(length);
        return string.Concat(bytes.Select(b => alphabet[b % alphabet.Length]));
    }
}
