namespace BullEvents.Api.Models;

/// <summary>
/// How much of the company's data a role may see.
///
/// Ordered from narrowest to widest so a comparison is enough to answer "does
/// this role see at least as much as that one" — the check the hierarchy and
/// the sharing rules both need.
/// </summary>
public enum DataScope
{
    /// <summary>Records this user owns, and nothing else.</summary>
    Own = 0,

    /// <summary>Their own records plus everyone reporting to them, at any depth.</summary>
    Team = 1,

    /// <summary>Every record in the company they belong to.</summary>
    Company = 2,

    /// <summary>Every record in every company. Platform operators only.</summary>
    Platform = 3,
}

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string CompanyAdmin = "CompanyAdmin";
    public const string Agm = "AGM";
    public const string SalesManager = "SalesManager";
    public const string SalesExecutive = "SalesExecutive";
    public const string TeleSales = "TeleSales";
    public const string PostSales = "PostSales";
    public const string BackOffice = "BackOffice";
    public const string Mis = "MIS";
    public const string Hr = "HR";
    public const string Employee = "Employee";

    /* ---------------- retired names ---------------- */
    //
    // Kept as aliases because they are spelled into [Authorize] attributes in
    // forty-odd places and into rows already in the database. Each points at the
    // role that replaced it, so old and new spellings authorise identically
    // while the call sites are migrated.

    public const string BranchManager = Agm;
    public const string TeamLead = SalesManager;
    public const string SalesAgent = SalesExecutive;
    public const string AccountsFinance = BackOffice;
    public const string ChannelPartner = "ChannelPartner";

    public static readonly string[] All =
    [
        SuperAdmin, CompanyAdmin, Agm, SalesManager, SalesExecutive,
        TeleSales, PostSales, BackOffice, Mis, Hr, Employee, ChannelPartner,
    ];
}

/// <summary>
/// What one role is allowed to do and see.
///
/// Held as a catalogue in code rather than as rows a screen can edit, because
/// these are the shapes the product is built around: a screen that let someone
/// give Tele Sales company-wide scope would be offering a promise the query
/// layer does not keep. What the console *does* edit is which modules a
/// particular person gets and who they report to — the two things that vary per
/// company without changing what a role means.
/// </summary>
public record RoleDefinition(
    string Key,
    string Name,
    string Description,
    DataScope Scope,
    /// <summary>Module keys this role opens by default.</summary>
    string[] Modules,
    /// <summary>Sees only closed-won business rather than the open pipeline.</summary>
    bool WonBusinessOnly = false,
    /// <summary>Reads widely but writes nothing — reporting seats.</summary>
    bool ReadOnly = false,
    /// <summary>Reaches the people records rather than the sales ones.</summary>
    bool PeopleData = false);

/// <summary>
/// Every module a role can be given. These are the app and section keys the
/// navigation is built from, so granting one here is what makes it appear.
/// </summary>
public static class Modules
{
    public const string Leads = "leads";
    public const string Engagement = "engagement";
    public const string Automation = "automation";
    public const string Calls = "calls";
    public const string Sales = "sales";
    public const string Inventory = "inventory";
    public const string Reports = "reports";
    public const string Calendar = "calendar";
    public const string Goals = "goals";
    public const string PostSales = "post-sales";
    public const string CustomerCare = "customer-care";
    public const string Constructions = "constructions";
    public const string Hr = "hr";
    public const string Administration = "administration";
    public const string SystemConsole = "system-console";

    public static readonly string[] All =
    [
        Leads, Engagement, Automation, Calls, Sales, Inventory, Reports,
        Calendar, Goals, PostSales, CustomerCare, Constructions, Hr,
        Administration, SystemConsole,
    ];

    /// <summary>What a working sales seat needs, and nothing beyond it.</summary>
    public static readonly string[] SalesDesk =
        [Leads, Engagement, Calls, Sales, Inventory, Calendar, Goals];

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Leads] = "Leads",
        [Engagement] = "Engagement",
        [Automation] = "Lead Automation",
        [Calls] = "Calls",
        [Sales] = "Sales",
        [Inventory] = "Inventory",
        [Reports] = "Reports",
        [Calendar] = "Calendar",
        [Goals] = "Goals",
        [PostSales] = "Post Sales",
        [CustomerCare] = "Customer Care",
        [Constructions] = "Constructions",
        [Hr] = "HR",
        [Administration] = "Administration",
        [SystemConsole] = "System Console",
    };

    /// <summary>The module's name in English, for anything a person reads.</summary>
    public static string Label(string module) =>
        Labels.TryGetValue(module, out var label) ? label : module;
}

public static class RoleCatalog
{
    private static readonly RoleDefinition[] Definitions =
    [
        new(Roles.SuperAdmin, "Platform Admin",
            "Operates the platform. Sees every company and every record in it.",
            DataScope.Platform,
            Modules.All),

        new(Roles.CompanyAdmin, "Company Admin",
            "Runs one company. Sees everything inside it and nothing outside.",
            DataScope.Company,
            [.. Modules.All.Except([Modules.SystemConsole])]),

        new(Roles.Agm, "AGM",
            "Carries a team. Sees their own work and everyone reporting to them, "
            + "however deep the reporting line runs.",
            DataScope.Team,
            [.. Modules.SalesDesk, Modules.Automation, Modules.Reports, Modules.PostSales, Modules.Hr]),

        new(Roles.SalesManager, "Sales Manager",
            "Works their own pipeline.",
            DataScope.Own,
            [.. Modules.SalesDesk, Modules.Automation, Modules.Reports]),

        new(Roles.SalesExecutive, "Sales Executive",
            "Works their own pipeline.",
            DataScope.Own,
            Modules.SalesDesk),

        new(Roles.TeleSales, "Tele Sales",
            "Works their own calling list.",
            DataScope.Own,
            [Modules.Leads, Modules.Calls, Modules.Engagement, Modules.Calendar]),

        new(Roles.PostSales, "Post Sales",
            "Takes over once a deal is won. Sees closed business only — the open "
            + "pipeline is not theirs to work.",
            DataScope.Company,
            [Modules.PostSales, Modules.Sales, Modules.Inventory, Modules.CustomerCare, Modules.Reports],
            WonBusinessOnly: true),

        new(Roles.BackOffice, "Back Office",
            "Reads the whole company for reporting. Writes nothing.",
            DataScope.Company,
            [.. Modules.All.Except([Modules.SystemConsole, Modules.Administration])],
            ReadOnly: true),

        new(Roles.Mis, "MIS",
            "Reads the whole company for reporting. Writes nothing.",
            DataScope.Company,
            [.. Modules.All.Except([Modules.SystemConsole, Modules.Administration, Modules.Hr])],
            ReadOnly: true),

        new(Roles.Hr, "HR",
            "Holds the people records for the company. No access to the sales pipeline.",
            DataScope.Company,
            [Modules.Hr, Modules.Administration, Modules.Calendar, Modules.Reports],
            PeopleData: true),

        new(Roles.Employee, "Employee",
            "Self-service: own profile, attendance, leave and payslips.",
            DataScope.Own,
            [Modules.Hr, Modules.Calendar],
            PeopleData: true),

        new(Roles.ChannelPartner, "Channel Partner",
            "An outside broker. Sees only what they themselves registered.",
            DataScope.Own,
            [Modules.Leads, Modules.Inventory]),
    ];

    private static readonly Dictionary<string, RoleDefinition> ByKey =
        Definitions.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<RoleDefinition> All => Definitions;

    /// <summary>
    /// The definition for a role, falling back to the narrowest possible seat.
    ///
    /// An unrecognised role resolving to "own records, sales desk only" is
    /// deliberate: a typo in a role name, or a row left behind by a rename,
    /// should cost someone access rather than hand them the company.
    /// </summary>
    public static RoleDefinition For(string? role) =>
        role is not null && ByKey.TryGetValue(role, out var found)
            ? found
            : ByKey[Roles.SalesExecutive];

    public static bool Exists(string? role) =>
        role is not null && ByKey.ContainsKey(role);
}
