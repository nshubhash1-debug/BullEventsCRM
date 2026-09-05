using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>One record that already looks like the one being saved.</summary>
public record DuplicateRuleMatch(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string Stage,
    string? OwnerName,
    DateTime CreatedAt,
    /// <summary>Which fields matched, so the person can judge it themselves.</summary>
    string[] MatchedOn);

/// <summary>What a rule says to do about the matches it found.</summary>
public record DuplicateVerdict(string Action, string RuleName, IReadOnlyList<DuplicateRuleMatch> Matches)
{
    public bool Blocks => Action == DuplicateActions.Block && Matches.Count > 0;
    public bool Warns => Action == DuplicateActions.Warn && Matches.Count > 0;
}

/// <summary>
/// Finds the record that is already there.
///
/// Matched on exact field equality rather than on a similarity score. A fuzzy
/// matcher looks impressive and then flags two brothers at one address as the
/// same person, and a duplicate rule that produces false positives is a rule
/// somebody switches off within the week. Phone and email are the fields a real
/// duplicate actually shares, and they compare exactly.
///
/// Digits only for phones: the same number arrives as "+91 98765 43210",
/// "9876543210" and "098765-43210" from three different sources, and a
/// string comparison would call those three different people.
/// </summary>
public class DuplicateRuleChecker(AppDbContext db)
{
    /// <summary>
    /// Checks a lead about to be saved against the active rules.
    ///
    /// <paramref name="excludeId"/> is the record being edited, so a lead never
    /// counts as a duplicate of itself.
    /// </summary>
    public async Task<DuplicateVerdict?> CheckLeadAsync(
        string? name,
        string? phone,
        string? email,
        int? excludeId = null,
        DateTime? eventDate = null,
        CancellationToken ct = default)
    {
        var rules = await db.DuplicateRules
            .Where(r => r.IsActive && r.Object == SecuredObjects.Lead)
            .ToListAsync(ct);

        if (rules.Count == 0) return null;

        foreach (var rule in rules)
        {
            var fields = rule.MatchFields
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(f => f.ToLowerInvariant())
                .ToHashSet();

            var wantsPhone = fields.Contains("phone");
            var wantsEmail = fields.Contains("email");
            var wantsName = fields.Contains("name");

            var normalisedPhone = Digits(phone);

            // A rule whose fields are all blank on this record cannot match
            // anything, and running it would scan the table for nothing.
            if (wantsPhone && normalisedPhone is null) continue;
            if (wantsEmail && string.IsNullOrWhiteSpace(email)) continue;
            if (wantsName && string.IsNullOrWhiteSpace(name)) continue;
            if (!wantsPhone && !wantsEmail && !wantsName) continue;

            var query = db.Leads.AsQueryable();

            if (excludeId is int id) query = query.Where(l => l.Id != id);

            // Phone is compared on digits, in the database, by stripping the
            // separators people actually type. Doing it in memory would mean
            // pulling every lead in the company back to compare eight of them.
            if (wantsPhone && normalisedPhone is not null)
            {
                // Collated to "C" before the tail comparison. The text columns in
                // this database carry a case-insensitive, non-deterministic
                // collation, and Postgres refuses substring searches against one
                // — the same trap that has already bitten the custom-field probe
                // and the booking-number sequence. Digits have no case, so
                // forcing the byte collation here costs nothing and is the only
                // thing that makes the comparison run at all.
                query = query.Where(l =>
                    l.Phone != null
                    && EF.Functions.Collate(l.Phone, "C")
                        .Replace(" ", "").Replace("-", "").Replace("+", "")
                        .Replace("(", "").Replace(")", "")
                        .EndsWith(normalisedPhone));
            }

            if (wantsEmail && !string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(l => l.Email == email);
            }

            if (wantsName && !string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(l => l.Name == name);
            }

            var matched = await query
                .Include(l => l.Owner)
                .OrderByDescending(l => l.CreatedAt)
                .Take(5)
                .Select(l => new DuplicateRuleMatch(
                    l.Id, l.Name, l.Phone, l.Email, l.Stage,
                    l.Owner == null ? null : l.Owner.Name,
                    l.CreatedAt,
                    fields.ToArray()))
                .ToListAsync(ct);

            if (matched.Count > 0)
            {
                return new DuplicateVerdict(rule.Action, rule.Name, matched);
            }
        }

        var occasion = await SameOccasionAsync(name, phone, email, eventDate, excludeId, ct);
        if (occasion is { Matches.Count: > 0 }) return occasion;

        return null;
    }

    /// <summary>
    /// The last ten digits of a phone number, or null when there are not ten.
    ///
    /// Ten because that is an Indian mobile number without its country code,
    /// and matching on the tail is what makes "+919876543210" and "9876543210"
    /// the same person.
    /// </summary>
    private static string? Digits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        return digits.Length >= 10 ? digits[^10..] : null;
    }

    private async Task<DuplicateVerdict?> SameOccasionAsync(
        string? name,
        string? phone,
        string? email,
        DateTime? eventDate,
        int? excludeId,
        CancellationToken ct)
    {
        if (eventDate is null) return null;

        var day = eventDate.Value.Date;
        var next = day.AddDays(1);
        var tail = Digits(phone);

        var query = db.Leads.AsQueryable()
            .Where(l => l.EventDate != null && l.EventDate >= day && l.EventDate < next
                && l.Stage != LeadStages.Lost);

        if (excludeId is int id) query = query.Where(l => l.Id != id);

        if (tail is not null)
        {
            query = query.Where(l =>
                l.Phone != null
                && EF.Functions.Collate(l.Phone, "C")
                    .Replace(" ", "").Replace("-", "").Replace("+", "")
                    .Replace("(", "").Replace(")", "")
                    .EndsWith(tail));
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(l => l.Email == email);
        }
        else
        {
            return null;
        }

        var matched = await query
            .Include(l => l.Owner)
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .Select(l => new DuplicateRuleMatch(
                l.Id, l.Name, l.Phone, l.Email, l.Stage,
                l.Owner == null ? null : l.Owner.Name,
                l.CreatedAt,
                new[] { "phone", "eventDate" }))
            .ToListAsync(ct);

        return matched.Count == 0
            ? null
            : new DuplicateVerdict(DuplicateActions.Block, "Same occasion", matched);
    }
}
