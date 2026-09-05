namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Custom fields
 * ------------------------------------------------------------------ */

/// <summary>What kind of value a custom field holds.</summary>
public enum CustomFieldType
{
    Text = 0,
    Number = 1,
    Date = 2,
    /// <summary>One of a fixed list, held in <see cref="CustomFieldDefinition.OptionsCsv"/>.</summary>
    Select = 3,
    Checkbox = 4,
    /// <summary>Longer free text — rendered as a textarea rather than an input.</summary>
    LongText = 5,
}

/// <summary>
/// A field one company added to one object.
///
/// The definition lives in a table and the value lives in a <c>jsonb</c> column
/// on the record itself. The alternative — a row per value in an
/// entity-attribute-value table — makes every list query a pile of joins and
/// every filter a special case, which is exactly the shape this CRM's query
/// engine exists to avoid.
///
/// Two real-estate companies never agree on the lead form. Without this, every
/// customer who wanted one extra field needed a migration and a deploy.
/// </summary>
public class CustomFieldDefinition : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>A key from <see cref="SecuredObjects"/> — which object this hangs off.</summary>
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// The property name inside the record's JSON. Generated from the label
    /// once, then frozen: renaming it would orphan every value already stored
    /// under the old name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public string? HelpText { get; set; }

    public CustomFieldType Type { get; set; } = CustomFieldType.Text;

    /// <summary>The choices, for a Select. Ignored by every other type.</summary>
    public string? OptionsCsv { get; set; }

    public bool Required { get; set; }

    /// <summary>Offered as a column on the record grid, not only on the form.</summary>
    public bool ShowInList { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Retired rather than deleted. Values already captured under this key stay
    /// in the records that hold them, and turning the field back on shows them
    /// again — which is what an administrator expects, and what a delete would
    /// quietly destroy.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public string[] Options =>
        OptionsCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}

/* ------------------------------------------------------------------ *
 * Pick lists
 * ------------------------------------------------------------------ */

/// <summary>
/// The named lists a company can edit — lead sources, loss reasons, and the
/// rest.
///
/// Stages are deliberately absent: the pipeline's order and its won/lost
/// endpoints are wired into forecasting, the conversion model and half the
/// dashboards. A company renaming "Qualified" is a label change the CRM can
/// take; a company inventing a stage between two others is not.
/// </summary>
public static class PickLists
{
    public const string LeadSource = "lead-source";
    public const string LossReason = "loss-reason";

    /// <summary>The occasion. Stored key kept; reads as "event types".</summary>
    public const string RequirementType = "requirement-type";

    /// <summary>How the client pays. Stored key kept; reads as "payment modes".</summary>
    public const string FundingMode = "funding-mode";

    public const string CallOutcome = "call-outcome";
    public const string VisitOutcome = "visit-outcome";

    /// <summary>What the planner is being asked to handle — venue, catering, décor.</summary>
    public const string ServiceCategory = "service-category";

    /// <summary>The functions in a run — mehendi, haldi, sangeet, reception.</summary>
    public const string FunctionType = "function-type";

    public static readonly string[] All =
    [
        LeadSource, LossReason, RequirementType, FundingMode, CallOutcome,
        VisitOutcome, ServiceCategory, FunctionType,
    ];

    public static string Label(string list) => list switch
    {
        LeadSource => "Lead sources",
        LossReason => "Loss reasons",
        RequirementType => "Event types",
        FundingMode => "Payment modes",
        CallOutcome => "Call outcomes",
        VisitOutcome => "Venue visit outcomes",
        ServiceCategory => "Service categories",
        FunctionType => "Function types",
        _ => list,
    };

    public static string Describe(string list) => list switch
    {
        LeadSource => "Where an enquiry came from. Feeds source-wise reporting and the attribution charts.",
        LossReason => "Why an enquiry was lost. The one list worth arguing over — it is what the loss analysis reads.",
        RequirementType => "The occasion being planned: wedding, reception, birthday, conference.",
        FundingMode => "How the client is settling the bill — self-funded, instalments, corporate PO.",
        CallOutcome => "How a call ended. Connected, no answer, wrong number.",
        VisitOutcome => "How a venue visit went, once the client has left.",
        ServiceCategory => "What the planner is being asked to handle — venue, catering, décor, photography.",
        FunctionType => "The functions in a wedding run — mehendi, haldi, sangeet, reception.",
        _ => string.Empty,
    };

    public static bool Exists(string? list) =>
        list is not null && All.Contains(list, StringComparer.OrdinalIgnoreCase);
}

/// <summary>One entry in one company's copy of one list.</summary>
public class PickListValue : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>A key from <see cref="PickLists"/>.</summary>
    public string List { get; set; } = string.Empty;

    /// <summary>
    /// What gets stored on the record. Frozen after creation for the same
    /// reason a custom field's key is — records already carry it.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>What people see. Renaming this is safe and expected.</summary>
    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Retired rather than deleted, so historic records still read correctly.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Shipped with the product. Renameable and retireable, not deletable —
    /// the seeders and the demo data reference these values by name.
    /// </summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
