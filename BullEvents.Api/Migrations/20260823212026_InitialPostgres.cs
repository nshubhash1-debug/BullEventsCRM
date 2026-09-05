using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:crm_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Entity = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EntityId = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Action = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    PlanTier = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    BrandColor = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LogoUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Developer = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Locality = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ReraNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LaunchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PossessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PriceMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Amenities = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ContactPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChargeHeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Group = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Basis = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DefaultQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeInSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    DueLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeHeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargeHeads_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StandardDiscount = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DiscountTolerance = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AssuredReturnPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AssuredReturnYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BuyBackPercentPerYear = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BuyBackEligibleAfterYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    IndicativeRentPerSqftPerMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnConditions = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Towers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FloorCount = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerFloor = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Towers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Towers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    EntityLabel = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Summary = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    RequestedByName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedById = table.Column<int>(type: "integer", nullable: true),
                    DecidedByName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approvals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CallLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RelatedType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    RelatedId = table.Column<int>(type: "integer", nullable: false),
                    RelatedName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Direction = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Outcome = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Disposition = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    WaitSeconds = table.Column<int>(type: "integer", nullable: true),
                    AgentId = table.Column<int>(type: "integer", nullable: true),
                    AgentName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RecordingUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SentimentScore = table.Column<double>(type: "double precision", nullable: true),
                    SentimentLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallLogs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObmVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitCode = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    PartnerName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PartnerType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ContactPerson = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ContactPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DistanceKm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpenseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Outcome = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    MeetingNotes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LeadsGenerated = table.Column<int>(type: "integer", nullable: false),
                    BusinessValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NextMeetingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgentId = table.Column<int>(type: "integer", nullable: true),
                    AgentName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObmVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObmVisits_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Salutation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FirstName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LastName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FullName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Designation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AccountName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone2 = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AltEmail = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    WhatsAppNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    State = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Country = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Pincode = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LifecycleStage = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Source = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Tags = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Segment = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LifetimeValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DealCount = table.Column<int>(type: "integer", nullable: false),
                    BudgetMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PreferredConfiguration = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PreferredLocality = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DoNotCall = table.Column<bool>(type: "boolean", nullable: false),
                    DoNotEmail = table.Column<bool>(type: "boolean", nullable: false),
                    WhatsAppOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    PanNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Gstin = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PreferredLanguage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnniversaryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    ConvertedFromLeadId = table.Column<int>(type: "integer", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contacts_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RelatedType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    RelatedId = table.Column<int>(type: "integer", nullable: false),
                    RelatedName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Channel = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Priority = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReminderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SlaMinutes = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowUps_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUps_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Dataset = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Measure = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Aggregation = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FilterJson = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DateField = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsRatio = table.Column<bool>(type: "boolean", nullable: false),
                    RatioDataset = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RatioMeasure = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RatioAggregation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RatioFilterJson = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RatioDateField = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Format = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PeriodType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScopeType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    TargetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Goals_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Salutation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CompanyName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone2 = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    State = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Pincode = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Country = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Zone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnniversaryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaritalStatus = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FatherOrSpouseName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Occupation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Designation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Nationality = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Source = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Stage = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SubStatus = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Priority = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    SupportingManagerId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BudgetMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequirementType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Configuration = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PreferredLocality = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PossessionTimelineMonths = table.Column<int>(type: "integer", nullable: true),
                    FundingMode = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ProductGroup = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    InterestedProjectId = table.Column<int>(type: "integer", nullable: true),
                    AreaRange = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Campaign = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    UtmSource = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    UtmMedium = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ReferredBy = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Tags = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsConverted = table.Column<bool>(type: "boolean", nullable: false),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedContactId = table.Column<int>(type: "integer", nullable: true),
                    ConvertedOpportunityId = table.Column<int>(type: "integer", nullable: true),
                    LossReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CachedScore = table.Column<int>(type: "integer", nullable: true),
                    CachedBand = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ScoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leads_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leads_Projects_InterestedProjectId",
                        column: x => x.InterestedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Users_SupportingManagerId",
                        column: x => x.SupportingManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => new { x.UserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_UserBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlanMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentPlanId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Basis = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Percent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    ConstructionStage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlanMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentPlanMilestones_PaymentPlans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PaymentPlanId = table.Column<int>(type: "integer", nullable: true),
                    DefaultDiscount = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultNotes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DefaultTermsAndConditions = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ValidDays = table.Column<int>(type: "integer", nullable: true),
                    ApplyDiscount = table.Column<bool>(type: "boolean", nullable: true),
                    IncludeAssuredReturn = table.Column<bool>(type: "boolean", nullable: true),
                    IncludeBuyBack = table.Column<bool>(type: "boolean", nullable: true),
                    IncludeRentalYield = table.Column<bool>(type: "boolean", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationTemplates_PaymentPlans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuotationTemplates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RateCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    TowerId = table.Column<int>(type: "integer", nullable: true),
                    UnitType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Label = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RatePerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateCards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RateCards_Towers_TowerId",
                        column: x => x.TowerId,
                        principalTable: "Towers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    TowerId = table.Column<int>(type: "integer", nullable: true),
                    UnitNumber = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Floor = table.Column<int>(type: "integer", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CarpetArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BuiltUpArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SuperArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AreaUnit = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Facing = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ViewType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Bathrooms = table.Column<int>(type: "integer", nullable: false),
                    Balconies = table.Column<int>(type: "integer", nullable: false),
                    ParkingSlots = table.Column<int>(type: "integer", nullable: false),
                    IsCornerUnit = table.Column<bool>(type: "boolean", nullable: false),
                    VastuCompliant = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FloorRisePremium = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlcCharges = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlcPerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeldByUserId = table.Column<int>(type: "integer", nullable: true),
                    HeldUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HoldReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BookedByContactId = table.Column<int>(type: "integer", nullable: true),
                    BookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookedByLeadId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SalesPersonId = table.Column<int>(type: "integer", nullable: true),
                    SalesPersonName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BookedQuotationId = table.Column<int>(type: "integer", nullable: true),
                    BlockReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Units_Towers_TowerId",
                        column: x => x.TowerId,
                        principalTable: "Towers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeadActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Remarks = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FromStage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToStage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    ActorName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivities_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationTemplateCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    ChargeHeadId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationTemplateCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationTemplateCharges_QuotationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QuotationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    UnitId = table.Column<int>(type: "integer", nullable: true),
                    Stage = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Source = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ForecastCategory = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCommission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    NextStep = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    NextStepDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LossReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CompetitorName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StageEnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastOpenStage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Opportunities_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Opportunities_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Opportunities_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    QuoteNumber = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    OpportunityId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    UnitId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CustomerEmail = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BillingAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentPlanId = table.Column<int>(type: "integer", nullable: true),
                    PaymentPlanName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TowerName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    UnitNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    UnitType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SaleableArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CarpetArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BuiltUpArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateCardId = table.Column<int>(type: "integer", nullable: true),
                    RateCardLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RatePerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlcPerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveRatePerSqft = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StandardDiscountPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargesBasic = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargesTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargesTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundableTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ScheduledTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountInWords = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GrandTotalInWords = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DiscountApplied = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ShowAssuredReturn = table.Column<bool>(type: "boolean", nullable: false),
                    AssuredReturnPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AssuredReturnYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AssuredReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShowBuyBack = table.Column<bool>(type: "boolean", nullable: false),
                    BuyBackPercentPerYear = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BuyBackEligibleAfterYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BuyBackHorizonYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BuyBackAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BuyBackValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShowRentalYield = table.Column<bool>(type: "boolean", nullable: false),
                    RentPerSqftPerMonth = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RentPerMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossRentalYield = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ReturnsTotalEarned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnOnInvestment = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ReturnHorizonYears = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ReturnConditions = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ApprovalStatus = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ApprovalId = table.Column<int>(type: "integer", nullable: true),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RejectionReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true),
                    NextFollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FollowUpNote = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LastFollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FollowUpCount = table.Column<int>(type: "integer", nullable: false),
                    OriginalValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotations_PaymentPlans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "PaymentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quotations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quotations_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quotations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SiteVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    VisitCode = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    OpportunityId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    UnitId = table.Column<int>(type: "integer", nullable: true),
                    VisitorName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    VisitorPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    VisitType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HostId = table.Column<int>(type: "integer", nullable: true),
                    HostName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TransportMode = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PickupLocation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Feedback = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    InterestLevel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    BudgetDiscussed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NextAction = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CancellationReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteVisits_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteVisits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SiteVisits_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UnitStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ToStatus = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    PartyName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitStatusHistories_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Metadata = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationActivities_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    ChargeHeadId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Group = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Basis = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityUnit = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BasicAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeInSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    DueLabel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationCharges_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationFollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Note = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Outcome = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    NextFollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedByName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationFollowUps_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Category = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLines_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Percent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BasicAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationMilestones_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationNegotiations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    RequestedDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OfferedDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CustomerDemand = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    OurResponse = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DeltaAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedByName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationNegotiations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationNegotiations_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationShareLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    LastViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseStatus = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CustomerComment = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedByName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationShareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationShareLinks_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_BranchId",
                table: "Approvals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_CompanyId_Status_RequestedAt",
                table: "Approvals",
                columns: new[] { "CompanyId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_EntityType_EntityId",
                table: "Approvals",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId_At",
                table: "AuditLogs",
                columns: new[] { "CompanyId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity_EntityId",
                table: "AuditLogs",
                columns: new[] { "Entity", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CompanyId",
                table: "Branches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_BranchId",
                table: "CallLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_CompanyId_AgentId",
                table: "CallLogs",
                columns: new[] { "CompanyId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_CompanyId_StartedAt",
                table: "CallLogs",
                columns: new[] { "CompanyId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CallLogs_RelatedType_RelatedId",
                table: "CallLogs",
                columns: new[] { "RelatedType", "RelatedId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeHeads_CompanyId_Code",
                table: "ChargeHeads",
                columns: new[] { "CompanyId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeHeads_CompanyId_ProjectId_SortOrder",
                table: "ChargeHeads",
                columns: new[] { "CompanyId", "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeHeads_ProjectId",
                table: "ChargeHeads",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_BranchId",
                table: "Contacts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId_LifecycleStage",
                table: "Contacts",
                columns: new[] { "CompanyId", "LifecycleStage" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId_Type",
                table: "Contacts",
                columns: new[] { "CompanyId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Email",
                table: "Contacts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OwnerId",
                table: "Contacts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Phone",
                table: "Contacts",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_BranchId",
                table: "FollowUps",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CompanyId_OwnerId",
                table: "FollowUps",
                columns: new[] { "CompanyId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CompanyId_Status_DueAt",
                table: "FollowUps",
                columns: new[] { "CompanyId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_OwnerId",
                table: "FollowUps",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_RelatedType_RelatedId",
                table: "FollowUps",
                columns: new[] { "RelatedType", "RelatedId" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_BranchId",
                table: "Goals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CompanyId_ScopeType_OwnerId",
                table: "Goals",
                columns: new[] { "CompanyId", "ScopeType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CompanyId_Status_EndDate",
                table: "Goals",
                columns: new[] { "CompanyId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerId",
                table: "Goals",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_CreatedAt",
                table: "LeadActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_LeadId",
                table: "LeadActivities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BranchId",
                table: "Leads",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId",
                table: "Leads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId_CreatedAt",
                table: "Leads",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId_OwnerId",
                table: "Leads",
                columns: new[] { "CompanyId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CompanyId_Stage",
                table: "Leads",
                columns: new[] { "CompanyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Email",
                table: "Leads",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_InterestedProjectId",
                table: "Leads",
                column: "InterestedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OwnerId",
                table: "Leads",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Phone",
                table: "Leads",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SupportingManagerId",
                table: "Leads",
                column: "SupportingManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_BranchId",
                table: "ObmVisits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_CompanyId_ScheduledAt",
                table: "ObmVisits",
                columns: new[] { "CompanyId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_CompanyId_Status",
                table: "ObmVisits",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_LeadId",
                table: "ObmVisits",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_VisitCode",
                table: "ObmVisits",
                column: "VisitCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_BranchId",
                table: "Opportunities",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CompanyId_ExpectedCloseDate",
                table: "Opportunities",
                columns: new[] { "CompanyId", "ExpectedCloseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CompanyId_OwnerId",
                table: "Opportunities",
                columns: new[] { "CompanyId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CompanyId_Stage",
                table: "Opportunities",
                columns: new[] { "CompanyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_ContactId",
                table: "Opportunities",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_OwnerId",
                table: "Opportunities",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_ProjectId",
                table: "Opportunities",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_UnitId",
                table: "Opportunities",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlanMilestones_PaymentPlanId",
                table: "PaymentPlanMilestones",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_CompanyId_Code",
                table: "PaymentPlans",
                columns: new[] { "CompanyId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_ProjectId",
                table: "PaymentPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_Code",
                table: "Projects",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_Status",
                table: "Projects",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationActivities_QuotationId",
                table: "QuotationActivities",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationCharges_QuotationId",
                table: "QuotationCharges",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationFollowUps_QuotationId",
                table: "QuotationFollowUps",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_QuotationId",
                table: "QuotationLines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationMilestones_QuotationId",
                table: "QuotationMilestones",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationNegotiations_QuotationId",
                table: "QuotationNegotiations",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_BranchId",
                table: "Quotations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CompanyId_ApprovalStatus",
                table: "Quotations",
                columns: new[] { "CompanyId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CompanyId_Status",
                table: "Quotations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_OwnerId",
                table: "Quotations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_PaymentPlanId",
                table: "Quotations",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ProjectId",
                table: "Quotations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuoteNumber",
                table: "Quotations",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_UnitId",
                table: "Quotations",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationShareLinks_QuotationId",
                table: "QuotationShareLinks",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationShareLinks_Token",
                table: "QuotationShareLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationTemplateCharges_TemplateId",
                table: "QuotationTemplateCharges",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationTemplates_PaymentPlanId",
                table: "QuotationTemplates",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationTemplates_ProjectId",
                table: "QuotationTemplates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RateCards_CompanyId_ProjectId_EffectiveFrom",
                table: "RateCards",
                columns: new[] { "CompanyId", "ProjectId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_RateCards_ProjectId",
                table: "RateCards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RateCards_TowerId",
                table: "RateCards",
                column: "TowerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_BranchId",
                table: "SiteVisits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_CompanyId_ScheduledAt",
                table: "SiteVisits",
                columns: new[] { "CompanyId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_CompanyId_Status",
                table: "SiteVisits",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_LeadId",
                table: "SiteVisits",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_ProjectId",
                table: "SiteVisits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_UnitId",
                table: "SiteVisits",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_VisitCode",
                table: "SiteVisits",
                column: "VisitCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Towers_ProjectId",
                table: "Towers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId_Configuration",
                table: "Units",
                columns: new[] { "CompanyId", "Configuration" });

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId_Status",
                table: "Units",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Units_ProjectId_UnitNumber",
                table: "Units",
                columns: new[] { "ProjectId", "UnitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_TowerId",
                table: "Units",
                column: "TowerId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitStatusHistories_UnitId",
                table: "UnitStatusHistories",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CallLogs");

            migrationBuilder.DropTable(
                name: "ChargeHeads");

            migrationBuilder.DropTable(
                name: "FollowUps");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "LeadActivities");

            migrationBuilder.DropTable(
                name: "ObmVisits");

            migrationBuilder.DropTable(
                name: "Opportunities");

            migrationBuilder.DropTable(
                name: "PaymentPlanMilestones");

            migrationBuilder.DropTable(
                name: "QuotationActivities");

            migrationBuilder.DropTable(
                name: "QuotationCharges");

            migrationBuilder.DropTable(
                name: "QuotationFollowUps");

            migrationBuilder.DropTable(
                name: "QuotationLines");

            migrationBuilder.DropTable(
                name: "QuotationMilestones");

            migrationBuilder.DropTable(
                name: "QuotationNegotiations");

            migrationBuilder.DropTable(
                name: "QuotationShareLinks");

            migrationBuilder.DropTable(
                name: "QuotationTemplateCharges");

            migrationBuilder.DropTable(
                name: "RateCards");

            migrationBuilder.DropTable(
                name: "SiteVisits");

            migrationBuilder.DropTable(
                name: "UnitStatusHistories");

            migrationBuilder.DropTable(
                name: "UserBranches");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "QuotationTemplates");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PaymentPlans");

            migrationBuilder.DropTable(
                name: "Towers");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
