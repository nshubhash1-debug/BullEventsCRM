namespace BullEvents.Api.Models;

/// <summary>
/// The construction milestones a payment plan can be linked to.
///
/// A construction-linked instalment must not be demanded because a date passed
/// — it is demanded because the work was done. That is the whole difference
/// between a CLP and a time-based plan, and it is the reason this list exists as
/// vocabulary rather than as free text on each milestone: the bulk demand run
/// asks "which bookings are waiting on the fifth slab", and it can only answer
/// that if every booking spells the fifth slab the same way.
///
/// Held in code, like the role and plan catalogues. A company that invents a
/// stage would invent it on one plan and not the next, and the run that raises
/// forty demands would quietly skip half of them.
/// </summary>
public static class ConstructionStages
{
    public const string Excavation = "Excavation";
    public const string Foundation = "Foundation";
    public const string Plinth = "Plinth";

    /// <summary>Slab-wise stages are the bulk of a linked plan and the most demanded.</summary>
    public const string Slab = "Slab";

    public const string Superstructure = "Superstructure";
    public const string Brickwork = "Brickwork";
    public const string Plaster = "Plaster";
    public const string Flooring = "Flooring";
    public const string Plumbing = "Plumbing & Electrical";
    public const string Doors = "Doors & Windows";
    public const string Painting = "Painting";
    public const string ExternalWorks = "External Development";

    /// <summary>The occupancy certificate — the gate before possession can be offered.</summary>
    public const string OccupancyCertificate = "Occupancy Certificate";

    public const string Possession = "Possession";

    public static readonly string[] All =
    [
        Excavation, Foundation, Plinth, Slab, Superstructure, Brickwork,
        Plaster, Flooring, Plumbing, Doors, Painting, ExternalWorks,
        OccupancyCertificate, Possession,
    ];

    /// <summary>
    /// The stage a milestone's own label refers to, if any.
    ///
    /// The plan writes it into the label — "On casting of 5th slab", "On
    /// completion of brickwork" — because that is what the customer signs.
    /// Reading it back out is what lets a slab cast on Tower B raise every
    /// instalment waiting on it, rather than somebody opening forty bookings.
    ///
    /// Deliberately conservative: a label it cannot place returns null and that
    /// instalment stays date-driven, which is the safe failure. Guessing wrong
    /// bills a customer for work that has not happened.
    /// </summary>
    public static string? Detect(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        var text = label.ToLowerInvariant();

        // These are events on the paperwork, not on the site. A plan's booking
        // and agreement instalments read as stages otherwise.
        if (text.Contains("booking") || text.Contains("eoi") || text.Contains("token")
            || text.Contains("application") || text.Contains("agreement")
            || text.Contains("registration"))
        {
            return null;
        }

        if (text.Contains("slab") || text.Contains("casting")) return Slab;
        if (text.Contains("possession") || text.Contains("handover")) return Possession;
        if (text.Contains("oc") && text.Contains("certificate")) return OccupancyCertificate;
        if (text.Contains("occupancy")) return OccupancyCertificate;

        return All.FirstOrDefault(stage =>
            text.Contains(stage.Split(' ')[0].ToLowerInvariant()));
    }
}
