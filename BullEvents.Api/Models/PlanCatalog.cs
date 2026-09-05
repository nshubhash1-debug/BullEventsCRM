namespace BullEvents.Api.Models;

/// <summary>
/// What one plan includes.
///
/// Held in code rather than in a table, for the same reason the role catalogue
/// is: these are the shapes the product is sold in, and a screen that let
/// somebody invent a plan would be offering a promise nothing enforces. What a
/// contract genuinely varies — an enterprise buyer who negotiated two hundred
/// seats on Professional — is a per-company override on the company row, which
/// is the exception rather than a new plan.
/// </summary>
/// <param name="Seats">Active users allowed. <see cref="Unlimited"/> for no cap.</param>
/// <param name="Leads">Lead records allowed.</param>
/// <param name="Branches">Offices allowed.</param>
public record PlanDefinition(
    string Key,
    string Name,
    string Tagline,
    int Seats,
    int Leads,
    int Branches,
    string[] Modules,
    bool ApiAccess,
    bool Webhooks,
    bool CustomFields,
    bool Sso,
    /// <summary>Rupees per seat per month, shown on the subscription screen.</summary>
    int PricePerSeat)
{
    public bool Includes(string module) =>
        Modules.Contains(module, StringComparer.OrdinalIgnoreCase);
}

public static class PlanCatalog
{
    /// <summary>The value a limit takes when there is not one.</summary>
    public const int Unlimited = -1;

    public const string Starter = "Starter";
    public const string Growth = "Growth";
    public const string Professional = "Professional";
    public const string Enterprise = "Enterprise";

    private static readonly string[] StarterModules =
        [Modules.Leads, Modules.Engagement, Modules.Calls, Modules.Calendar, Modules.Reports];

    private static readonly string[] GrowthModules =
        [.. StarterModules, Modules.Sales, Modules.Inventory, Modules.Goals, Modules.Automation];

    private static readonly string[] ProfessionalModules =
        [.. GrowthModules, Modules.PostSales, Modules.CustomerCare, Modules.Administration, Modules.Hr];

    private static readonly PlanDefinition[] Definitions =
    [
        new(Starter, "Starter",
            "One office finding its feet.",
            Seats: 5, Leads: 2_000, Branches: 1,
            StarterModules,
            ApiAccess: false, Webhooks: false, CustomFields: false, Sso: false,
            PricePerSeat: 599),

        new(Growth, "Growth",
            "A sales floor with inventory to sell against.",
            Seats: 25, Leads: 25_000, Branches: 3,
            GrowthModules,
            ApiAccess: false, Webhooks: true, CustomFields: false, Sso: false,
            PricePerSeat: 999),

        new(Professional, "Professional",
            "Several branches, post-sales, and the paperwork that follows.",
            Seats: 100, Leads: 200_000, Branches: 15,
            ProfessionalModules,
            ApiAccess: true, Webhooks: true, CustomFields: true, Sso: false,
            PricePerSeat: 1_499),

        new(Enterprise, "Enterprise",
            "Everything, with the limits negotiated rather than published.",
            Seats: Unlimited, Leads: Unlimited, Branches: Unlimited,
            [.. Modules.All.Except([Modules.SystemConsole])],
            ApiAccess: true, Webhooks: true, CustomFields: true, Sso: true,
            PricePerSeat: 2_499),
    ];

    private static readonly Dictionary<string, PlanDefinition> ByKey =
        Definitions.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PlanDefinition> All => Definitions;

    public static readonly string[] Keys = [.. Definitions.Select(p => p.Key)];

    /// <summary>
    /// The plan for a key, falling back to the smallest one.
    ///
    /// Deliberately the smallest rather than the largest: a typo in a plan name
    /// should cost a company features they can ask for, not quietly hand them a
    /// tier nobody sold.
    /// </summary>
    public static PlanDefinition For(string? key) =>
        key is not null && ByKey.TryGetValue(key, out var plan) ? plan : ByKey[Starter];

    public static bool Exists(string? key) => key is not null && ByKey.ContainsKey(key);

    /// <summary>Where a plan sits in the ladder, for "is this an upgrade" questions.</summary>
    public static int Rank(string? key) =>
        Array.FindIndex(Definitions, p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The things a plan can cap, named so an error message can say which one.</summary>
public static class Limits
{
    public const string Seats = "seats";
    public const string Leads = "leads";
    public const string Branches = "branches";

    public static string Label(string limit) => limit switch
    {
        Seats => "user seats",
        Leads => "lead records",
        Branches => "branches",
        _ => limit,
    };
}
