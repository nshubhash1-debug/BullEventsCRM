namespace BullEvents.Api.Models;

/// <summary>What a connector is for, so the catalogue groups the way people shop.</summary>
public static class ConnectorCategories
{
    public const string Portals = "Property portals";
    public const string Advertising = "Advertising";
    public const string Telephony = "Telephony";
    public const string Messaging = "Messaging";
    public const string Web = "Web";

    public static readonly string[] All =
        [Portals, Advertising, Telephony, Messaging, Web];
}

/// <summary>Which way the data moves. Shown on the card, because it sets expectations.</summary>
public enum ConnectorDirection
{
    /// <summary>They push to us — a portal feed, a lead-ad webhook, a website form.</summary>
    Inbound = 0,

    /// <summary>We push to them — a WhatsApp send, a click-to-call.</summary>
    Outbound = 1,

    Both = 2,
}

/// <summary>
/// One credential a provider needs.
///
/// Declared rather than hard-coded into a form, so adding a provider is a row
/// in this catalogue instead of a new screen. <see cref="Secret"/> decides both
/// how it renders and whether it ever comes back out — a field marked secret is
/// write-only from the client's point of view.
/// </summary>
public record CredentialField(
    string Key,
    string Label,
    string Help,
    bool Secret = false,
    bool Required = true);

/// <summary>
/// A system this CRM can talk to.
///
/// The catalogue is in code for the same reason the role and plan catalogues
/// are: these are shapes the product supports, and a screen that let somebody
/// invent one would offer a promise nothing keeps. What a company configures is
/// an instance — credentials, which branch its leads land in, who owns them.
/// </summary>
public record ConnectorDefinition(
    string Provider,
    string Name,
    string Category,
    string Tagline,
    /// <summary>What connecting it actually does, in the admin's terms.</summary>
    string Description,
    ConnectorDirection Direction,
    IReadOnlyList<CredentialField> Credentials,
    /// <summary>
    /// True when the provider delivers by calling a URL we give them. Those get
    /// an inbound endpoint and a token; the rest are configured by credentials
    /// alone.
    /// </summary>
    bool HasInboundEndpoint,
    /// <summary>What to do on the provider's side, in order.</summary>
    IReadOnlyList<string> SetupSteps,
    /// <summary>False while the outbound half is still unimplemented.</summary>
    bool Live = true);

public static class ConnectorCatalog
{
    public const string MyOperator = "myoperator";
    public const string WedMeGood = "wedmegood";
    public const string ShaadiSaga = "shaadisaga";
    public const string VenueLook = "venuelook";
    public const string MetaLeadAds = "meta-lead-ads";
    public const string GoogleLeadForms = "google-lead-forms";
    public const string WhatsApp = "whatsapp";
    public const string Website = "website";

    private static readonly ConnectorDefinition[] Definitions =
    [
        /* ---------------- event portals ---------------- */

        new(WedMeGood, "WedMeGood", ConnectorCategories.Portals,
            "Enquiries from your WedMeGood listing, as they happen.",
            "WedMeGood posts each enquiry to the URL below. It arrives as a lead in the "
            + "branch and against the owner you choose here, filed under the source you "
            + "pick — so portal volume shows up in source reporting instead of as "
            + "near-duplicates of the word WedMeGood.",
            ConnectorDirection.Inbound,
            [
                new("vendorId", "Vendor ID", "The vendor id on your WedMeGood listing.", Required: false),
                new("sharedSecret", "Shared secret", "Optional. When set, a delivery without it is refused.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Sign in to the WedMeGood vendor dashboard.",
                "Open Leads, then API / Push integration.",
                "Paste the endpoint URL below as the delivery target.",
                "Send a test enquiry and check it appears in the activity log here.",
            ]),

        new(ShaadiSaga, "ShaadiSaga", ConnectorCategories.Portals,
            "Enquiries from your ShaadiSaga listing.",
            "The same arrangement as WedMeGood: ShaadiSaga calls the URL below with each "
            + "enquiry and the lead lands with the defaults you set here.",
            ConnectorDirection.Inbound,
            [
                new("vendorId", "Vendor ID", "Your ShaadiSaga vendor code.", Required: false),
                new("sharedSecret", "Shared secret", "Optional. When set, a delivery without it is refused.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Open the ShaadiSaga vendor console.",
                "Find Lead Integration, then Webhook or Push API.",
                "Paste the endpoint URL below.",
                "Send a test enquiry and confirm it lands.",
            ]),

        new(VenueLook, "VenueLook", ConnectorCategories.Portals,
            "Venue and banquet enquiries from your VenueLook listings.",
            "VenueLook posts each enquiry to the URL below, and it becomes a lead with the "
            + "branch, owner and source you choose here.",
            ConnectorDirection.Inbound,
            [
                new("partnerId", "Partner ID", "Your VenueLook partner account id.", Required: false),
                new("sharedSecret", "Shared secret", "Optional. When set, a delivery without it is refused.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Open the VenueLook partner dashboard.",
                "Go to Leads, then Integrations.",
                "Paste the endpoint URL below as the push target.",
                "Send a test enquiry and confirm it lands.",
            ]),

        /* ---------------- advertising ---------------- */

        new(MetaLeadAds, "Meta Lead Ads", ConnectorCategories.Advertising,
            "Facebook and Instagram lead forms, straight into the pipeline.",
            "Meta delivers each submitted lead form to the URL below. The form's own "
            + "fields are read by name, so a field you called \"budget\" on Facebook "
            + "lands in the budget on the lead — including into fields you added "
            + "yourself under Object Manager.",
            ConnectorDirection.Inbound,
            [
                new("pageId", "Page ID", "The Facebook page the forms belong to.", Required: false),
                new("verifyToken", "Verify token", "Meta echoes this back when it first subscribes. Any string you choose.", Secret: true, Required: false),
                new("appSecret", "App secret", "Used to check the X-Hub-Signature on each delivery.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "In Meta Business Suite, open the app connected to your page.",
                "Add a Webhooks subscription for the leadgen field.",
                "Use the endpoint URL below as the callback, and the verify token you set here.",
                "Submit a test lead from the form preview.",
            ]),

        new(GoogleLeadForms, "Google Lead Forms", ConnectorCategories.Advertising,
            "Lead form extensions from your Google Ads campaigns.",
            "Google posts each lead form submission to the URL below. Campaign and "
            + "form name arrive with it and are kept on the lead's attribution fields.",
            ConnectorDirection.Inbound,
            [
                new("customerId", "Customer ID", "Your Google Ads account id.", Required: false),
                new("googleKey", "Webhook key", "The key you set on the lead form extension.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Open the lead form extension in Google Ads.",
                "Under Lead delivery, choose Webhook.",
                "Paste the endpoint URL below and set a webhook key.",
                "Use Google's Send test data button.",
            ]),

        /* ---------------- telephony ---------------- */

        new(MyOperator, "MyOperator", ConnectorCategories.Telephony,
            "Cloud telephony: call logs, recordings and click-to-call.",
            "Incoming and outgoing calls are logged against the matching lead by phone "
            + "number, with the recording link and the agent who took it. A call from a "
            + "number nobody has becomes a new lead rather than a missed one.",
            ConnectorDirection.Both,
            [
                new("apiKey", "API key", "From the MyOperator dashboard, under Developers.", Secret: true),
                new("companyId", "Company ID", "Your MyOperator account identifier."),
            ],
            HasInboundEndpoint: true,
            [
                "Open the MyOperator panel and go to Developers, then API.",
                "Copy the API key and company id into the fields here.",
                "Add the endpoint URL below as the call-event webhook.",
                "Place a test call and confirm it appears in the activity log.",
            ]),

        /* ---------------- messaging ---------------- */

        new(WhatsApp, "WhatsApp Business", ConnectorCategories.Messaging,
            "Send template messages and receive replies on the lead's timeline.",
            "The queued WhatsApp touchpoints the automation screens already create are "
            + "sent through this connection, and replies land back on the same lead.",
            ConnectorDirection.Both,
            [
                new("phoneNumberId", "Phone number ID", "From the WhatsApp Business platform."),
                new("accessToken", "Access token", "A permanent token for the system user.", Secret: true),
                new("verifyToken", "Verify token", "Echoed back when Meta subscribes. Any string you choose.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Create a system user and a permanent token in Meta Business Suite.",
                "Copy the phone number id and token here.",
                "Add the endpoint URL below as the messages webhook.",
                "Send a message to your business number to test the inbound half.",
            ],
            // Inbound replies and the send path both need a live provider account.
            // Marked so the screen says which half works rather than implying both.
            Live: false),

        /* ---------------- web ---------------- */

        new(Website, "Website & web-to-lead", ConnectorCategories.Web,
            "A form on any site posts straight into the CRM.",
            "Point any form, landing page or web application at the URL below and its "
            + "submissions become leads. Fields are matched by name, so a form asking "
            + "for \"budget\" or a field you added yourself needs no mapping.",
            ConnectorDirection.Inbound,
            [
                new("allowedOrigins", "Allowed origins", "Comma-separated sites that may post here. Empty allows any.", Required: false),
                new("sharedSecret", "Shared secret", "Optional. When set, a delivery without it is refused.", Secret: true, Required: false),
            ],
            HasInboundEndpoint: true,
            [
                "Copy the endpoint URL below.",
                "Post your form to it as JSON or as a normal form submission.",
                "Include at least a name and either a phone or an email.",
                "Send one test submission and check the activity log here.",
            ]),
    ];

    private static readonly Dictionary<string, ConnectorDefinition> ByKey =
        Definitions.ToDictionary(d => d.Provider, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ConnectorDefinition> All => Definitions;

    public static ConnectorDefinition? For(string? provider) =>
        provider is not null && ByKey.TryGetValue(provider, out var found) ? found : null;

    public static bool Exists(string? provider) =>
        provider is not null && ByKey.ContainsKey(provider);
}

/// <summary>Where a connector stands, as the card shows it.</summary>
public static class ConnectorStatuses
{
    public const string NotConfigured = "NotConfigured";
    public const string Connected = "Connected";
    public const string Error = "Error";

    public static readonly string[] All = [NotConfigured, Connected, Error];
}

/// <summary>What happened to one thing that arrived.</summary>
public static class ConnectorOutcomes
{
    public const string Created = "Created";

    /// <summary>Matched a lead that already existed. Not an error — the usual case on a retry.</summary>
    public const string Duplicate = "Duplicate";

    /// <summary>Arrived, but there was not enough in it to make a lead.</summary>
    public const string Rejected = "Rejected";

    /// <summary>Something on our side went wrong. The payload is kept so it can be replayed.</summary>
    public const string Failed = "Failed";
}
