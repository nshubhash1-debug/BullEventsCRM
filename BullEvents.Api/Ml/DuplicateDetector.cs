using System.Text;
using BullEvents.Api.Models;

namespace BullEvents.Api.Ml;

public record DuplicateMatch(
    int LeadId,
    string Name,
    string? Phone,
    string? Email,
    string Stage,
    string Reason,
    int Confidence
);

/// <summary>
/// Deterministic fuzzy matching for duplicate leads — the same enquiry arriving
/// twice from a portal and a channel partner is the common case in real estate.
///
/// Phone and email are normalised and compared exactly; names fall back to a
/// Jaro-Winkler similarity, which handles transpositions and shared prefixes far
/// better than edit distance for person names.
/// </summary>
public static class DuplicateDetector
{
    public static List<DuplicateMatch> FindDuplicates(
        Lead candidate,
        IEnumerable<Lead> pool,
        int limit = 5)
    {
        var candidatePhones = Phones(candidate);
        var candidateEmail = NormalizeEmail(candidate.Email);
        var candidateName = NormalizeName(candidate.Name);
        var candidateCity = NormalizeName(candidate.City);

        var matches = new List<DuplicateMatch>();

        foreach (var other in pool)
        {
            if (other.Id == candidate.Id) continue;

            string? reason = null;
            var confidence = 0;

            if (candidatePhones.Count > 0 && Phones(other).Overlaps(candidatePhones))
            {
                reason = "Same mobile number";
                confidence = 96;
            }
            else if (candidateEmail is not null && NormalizeEmail(other.Email) == candidateEmail)
            {
                reason = "Same email address";
                confidence = 94;
            }
            else if (candidateName.Length > 0)
            {
                var similarity = JaroWinkler(candidateName, NormalizeName(other.Name));
                var sameCity = candidateCity.Length > 0 &&
                               candidateCity == NormalizeName(other.City);

                if (similarity >= 0.94)
                {
                    reason = "Near-identical name";
                    confidence = (int)Math.Round(similarity * 88);
                }
                else if (similarity >= 0.86 && sameCity)
                {
                    reason = "Similar name in the same city";
                    confidence = (int)Math.Round(similarity * 80);
                }
            }

            if (reason is null) continue;

            matches.Add(new DuplicateMatch(
                other.Id,
                other.Name,
                other.Phone,
                other.Email,
                other.Stage,
                reason,
                confidence));
        }

        return matches
            .OrderByDescending(m => m.Confidence)
            .Take(limit)
            .ToList();
    }

    /* ------------------------------------------------------------------ *
     * Normalisation
     * ------------------------------------------------------------------ */

    /// <summary>Last 10 digits of each number on the record, so +91 / 0 prefixes collapse.</summary>
    private static HashSet<string> Phones(Lead lead)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in new[] { lead.Phone, lead.Phone2 })
        {
            var normalized = NormalizePhone(raw);
            if (normalized is not null) set.Add(normalized);
        }
        return set;
    }

    private static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new StringBuilder();
        foreach (var ch in raw)
        {
            if (char.IsDigit(ch)) digits.Append(ch);
        }

        if (digits.Length < 7) return null;
        var value = digits.ToString();
        return value.Length > 10 ? value[^10..] : value;
    }

    private static string? NormalizeEmail(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();

    private static string NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) builder.Append(ch);
            else if (char.IsWhiteSpace(ch) && builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
        }

        return builder.ToString().Trim();
    }

    /* ------------------------------------------------------------------ *
     * Jaro-Winkler
     * ------------------------------------------------------------------ */

    private static double JaroWinkler(string a, string b)
    {
        var jaro = Jaro(a, b);
        if (jaro < 0.7) return jaro;

        var prefix = 0;
        var max = Math.Min(4, Math.Min(a.Length, b.Length));
        while (prefix < max && a[prefix] == b[prefix]) prefix++;

        return jaro + prefix * 0.1 * (1 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;

        var window = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        var matches = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var start = Math.Max(0, i - window);
            var end = Math.Min(i + window + 1, b.Length);

            for (var j = start; j < end; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = true;
                bMatched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0;

        double transpositions = 0;
        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }
        transpositions /= 2;

        return (matches / (double)a.Length +
                matches / (double)b.Length +
                (matches - transpositions) / matches) / 3.0;
    }
}
