using System.Text;
using System.Threading.RateLimiting;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Ml;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

/* ------------------------------------------------------------------ *
 * Persistence
 * ------------------------------------------------------------------ */

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

// Resolved per request from the JWT, then read by the DbContext's global query
// filters — this is what enforces tenant isolation, in place of Postgres RLS.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options
        .UseNpgsql(connectionString, postgres =>
            postgres.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null))
        .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<OtpService>();
// Sharing widens what AccessScope allows, so it is resolved alongside it and
// cached for the same request.
// Sign-in windows and IP restrictions. Enforced at the door, so the model an
// administrator edits is the one the login path actually consults.
builder.Services.AddScoped<LoginPolicyGuard>();

// Lead routing and duplicate detection, both of which run on the create path.
builder.Services.AddScoped<AssignmentRuleEngine>();
builder.Services.AddScoped<DuplicateRuleChecker>();

// The SLA sweep, and the scheduler that drives it.
builder.Services.AddScoped<EscalationSweep>();

// The one place leave entitlement moves — every grant, carry-forward,
// compensatory day, encashment and lapse goes through it.
builder.Services.AddScoped<LeaveLedger>();

// Turns a pay structure's components into amounts, in dependency order.
builder.Services.AddScoped<SalaryStructureResolver>();
builder.Services.AddSingleton<JobRunner>();
builder.Services.AddHostedService<JobHost>();

builder.Services.AddScoped<SharingEngine>();
builder.Services.AddScoped<AccessScope>();
builder.Services.AddScoped<PermissionResolver>();

// Backs the two per-request checks a signed token cannot make about itself:
// whether its session is still wanted, and whether its tenant is still trading.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SessionService>();

// What a tenant's plan allows, and the daily counters behind the usage chart.
builder.Services.AddScoped<EntitlementService>();
builder.Services.AddScoped<CustomFieldService>();

// Integration surface: keys for the public API, and the webhook queue behind it.
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<WebhookDispatcher>();
builder.Services.AddScoped<WebhookSender>();
builder.Services.AddHostedService<WebhookHost>();

// A short timeout on purpose: a receiver that takes longer than this is one the
// retry ladder should handle, not one this process should wait for.
builder.Services.AddHttpClient("webhooks", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BullEventsCRM-Webhooks/1.0");
});
builder.Services.AddScoped<UsageRecorder>();
builder.Services.AddHostedService<SubscriptionHost>();

// SES only once a sender address is configured. Without one the sign-in code
// goes to the log and the response says it was not delivered, which is a
// working local setup and an obviously broken production one.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:FromAddress"]))
{
    builder.Services.AddSingleton<IEmailSender, SesEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
}
builder.Services.AddScoped<ApprovalService>();

// Post sales: the money that follows a sold unit.
// Reads once at startup and refuses to skip the sign-in code outside Development.
builder.Services.AddSingleton<SignInPolicy>();

// Post-sales: the document engine and the lifecycle workflows.
builder.Services.AddScoped<DocumentComposer>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<BookingLifecycleService>();

builder.Services.AddScoped<BookingLedger>();
builder.Services.AddScoped<GstEngine>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<BookingService>();

// Penal interest is the one figure that moves when nothing happens, so it needs
// a pass of its own rather than riding on the money events.
builder.Services.AddScoped<InterestAccrual>();
builder.Services.AddHostedService<InterestAccrualHost>();
builder.Services.AddScoped<BookingPlanService>();
builder.Services.AddScoped<SpaceBookingService>();
builder.Services.AddScoped<QuotationLifecycle>();

// QuestPDF's Community licence. Free for organisations under the revenue
// threshold the library publishes; above it the deployment needs a paid key.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

/* ------------------------------------------------------------------ *
 * Local intelligence
 *
 * Each model is a singleton: the fitted ML.NET pipeline and its prediction
 * engine are built once at startup and reused for every request, rather than
 * refitted per call.
 * ------------------------------------------------------------------ */

builder.Services.AddSingleton<LeadScoringService>();
builder.Services.AddSingleton<OpportunityWinModel>();
builder.Services.AddSingleton<SentimentService>();
builder.Services.AddSingleton<ForecastService>();
builder.Services.AddSingleton<SegmentationService>();
builder.Services.AddSingleton<AnomalyService>();

builder.Services.AddScoped<MlTrainingCoordinator>();
builder.Services.AddHostedService<ModelTrainingHost>();

/* ------------------------------------------------------------------ *
 * Authentication
 * ------------------------------------------------------------------ */

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BullEvents.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BullEvents.Client";

// Two ways this application can be published and still be wide open, both of
// which look like a successful deploy. Neither is caught by a health check, so
// they are caught here instead: the process refuses to start rather than come
// up serving real customer records from behind a known secret.
if (builder.Environment.IsProduction())
{
    if (jwtKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Jwt:Key is still the development signing key. Anyone holding it can mint "
            + "a token for any user. Set Jwt__Key to a generated secret.");
    }

    var otpAcknowledged = builder.Configuration.GetValue("Auth:AllowUndeliveredOtp", false);

    // Sign-in codes are real and random, but they still have to reach somebody.
    // With no sender configured they go to the log, which in production means
    // the second factor is whoever can read the log.
    if (!otpAcknowledged && string.IsNullOrWhiteSpace(builder.Configuration["Email:FromAddress"]))
    {
        throw new InvalidOperationException(
            "Email:FromAddress is not configured, so sign-in codes would be written to the "
            + "log instead of sent. Configure SES, or set Auth__AllowUndeliveredOtp=true to "
            + "deploy anyway.");
    }
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),

            // MapInboundClaims is off, so claims arrive under the short names the
            // token was written with. Without pointing the role/name claim types
            // at those short names, [Authorize(Roles = ...)] looks for the long
            // WS-Federation URI, finds nothing, and 403s every role-gated route.
            RoleClaimType = "role",
            NameClaimType = "name",
        };
    });

builder.Services.AddAuthorization();

/* ------------------------------------------------------------------ *
 * Transport
 * ------------------------------------------------------------------ */

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();

        // In development the Next.js server moves to whatever port is free, so
        // pinning a single localhost origin breaks the app for no security
        // benefit on a loopback interface. Production still uses the configured
        // list only.
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && uri.IsLoopback);
        }
    });
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Sign-in is the one endpoint worth throttling by IP; the rest sit behind a
// bearer token and a per-user budget.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 240,
                TokensPerPeriod = 120,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 20,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));
});

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services
    .AddControllers(options =>
    {
        // Field-level security is applied to the response rather than to each
        // DTO, so one filter covers every endpoint that returns a secured
        // object. It short-circuits for callers who have no field rules, which
        // is everybody until an administrator writes one.
        options.Filters.Add<FieldSecurityFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Timestamps leave here as UTC with a Z. Without it the browser reads an
        // offset-less string as local time and every "3 hours ago" is wrong by
        // the client's own timezone offset.
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });

builder.Services.AddOpenApi();

var app = builder.Build();

/* ------------------------------------------------------------------ *
 * Pipeline
 * ------------------------------------------------------------------ */

app.UseExceptionHandler();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();

    // Demonstration data is generated on every start unless it is switched off,
    // which is what makes it worth a flag rather than a delete: a company that
    // has cleaned its database and imported its own records would otherwise
    // find eight hundred sample leads back in it the next morning.
    var seedDemo = builder.Configuration.GetValue("Seed:Demo", false);

    // The seeded accounts' password comes from configuration. Nothing set means
    // a different random one per install, printed once here — a fresh install
    // stays usable without shipping a password everybody already knows.
    await DbSeeder.SeedAsync(
        db,
        seedDemo,
        builder.Configuration["Seed:AdminPassword"],
        message => app.Logger.LogWarning("{SeedPassword}", message));

    if (seedDemo)
    {
        await HistorySeeder.SeedAsync(db);
        await CrmSeeder.SeedAsync(db);
    }

    // The two live projects are not demonstration data — they are the stock the
    // company actually sells, and they seed either way.
    // The access model's shipped defaults, so the console has rows to show.
    await PermissionSeeder.SeedAsync(db);

    // Each tenant's own copy of the lists it can rename.
    await PickListSeeder.SeedAsync(db);

    // The letters the post-sales desk sends, as editable drafts. An empty
    // template library is a feature nobody switches on — writing a demand letter
    // from nothing, in HTML, is not what a collections manager does on a Tuesday.
    foreach (var tenantId in await db.Companies.IgnoreQueryFilters().Select(c => c.Id).ToListAsync())
    {
        await DocumentTemplateSeeder.SeedAsync(db, tenantId);

        // The job titles the floor actually uses. An empty designation list
        // means every new user form offers nothing, and nobody builds an org
        // chart from scratch on day one.
        await DesignationSeeder.SeedAsync(db, tenantId);

        await EventCommercialsSeeder.SeedAsync(db, tenantId);

        await HrSeeder.SeedAsync(db, tenantId);

        // HrSeeder only makes an employee per login, which on a fresh install is
        // two people with no salary between them. This adds the workforce an
        // events company actually has, so payroll has something to run on. It
        // backs off the moment the tenant has a handful of employees of its own.
        await HrWorkforceSeeder.SeedAsync(db, tenantId);
    }

    // Two venues and a dozen enquiries, so a fresh install opens on something
    // recognisable rather than empty screens. It refuses to run the moment a
    // venue exists, so it can never touch a tenant carrying real records —
    // which is why it is not behind the Seed:Demo flag that guards CrmSeeder's
    // 380-lead volume test set.
    await EventDemoSeeder.SeedAsync(db);

    // The décor godown register, imported from the team's own workbook. Refuses
    // to run once a single prop line exists, so it loads once and never again.
    await PropInventorySeeder.SeedAsync(db, app.Environment);

    // Suppliers, the crew roster, the fleet and a few kits. Runs after the prop
    // import because the kits are built out of what that loaded.
    await ResourceDemoSeeder.SeedAsync(db);

    // PF, ESI, state professional tax and the year's income-tax slabs. Refuses
    // to run once a statutory row exists, so rates somebody has tuned to their
    // own notifications are never overwritten.
    await StatutorySeeder.SeedAsync(db);

    // The leave year, the policies that fill it, and the wedding season nobody
    // may book off. Refuses to run once a leave period exists, so a company
    // that has set up its own leave year is never touched. Runs after the
    // workforce seeder because it grants the year to whoever is on the rolls.
    await LeaveSeeder.SeedAsync(db, new LeaveLedger(db));

    // The components an Indian payroll is built out of, and two structures made
    // of them. Nobody is assigned to a structure — that would change what people
    // are paid. The existing Basic/HRA/Allowances records keep running until an
    // employee is moved across deliberately.
    await SalaryComponentSeeder.SeedAsync(db);

    // Interview rounds with something to assess, appraisal templates with
    // weighted responsibilities, and the season's staffing plan. Creates no
    // requisition, offer or appraisal — those are decisions, and inventing them
    // would put words in somebody's mouth.
    await TalentSeeder.SeedAsync(db);

    // The joining and leaving checklists, the skills a site is staffed from,
    // and what may be claimed back. Applies no checklist to anybody — putting a
    // separation list on a live employee would be an alarming thing for a
    // seeder to do.
    await WorkplaceSeeder.SeedAsync(db);

    // First fit runs inline so the very first request already has scores; the
    // hosted service takes over the periodic refits from there.
    var coordinator = scope.ServiceProvider.GetRequiredService<MlTrainingCoordinator>();
    await coordinator.TrainAllAsync();
}

app.UseCors("Frontend");

// Prop photographs, served straight off disk under /props/full and /props/thumb.
// Before authentication on purpose: an <img> tag carries no bearer token, and a
// picture of a brass pot is not what the tenant filter is protecting.
app.UseStaticFiles();

// Outside development only. The dev host is bound to HTTP alone, so the
// middleware cannot work out a port to redirect to and logs a warning on every
// request it handles — noise that trains the reader to ignore the log.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();

// Sits between authentication and authorization: the principal exists by now,
// and everything downstream — including the DbContext — sees the tenant.
// Before tenant resolution, because an API key is what decides which tenant the
// request belongs to.
app.UseMiddleware<ApiKeyMiddleware>();

app.UseMiddleware<TenantResolutionMiddleware>();

// Then the two things the token cannot say about itself. Before authorization,
// so a withdrawn session or a suspended tenant is refused whatever the action
// would otherwise have allowed.
app.UseMiddleware<SessionGuardMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
