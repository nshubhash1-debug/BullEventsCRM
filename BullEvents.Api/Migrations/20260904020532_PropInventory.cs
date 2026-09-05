using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class PropInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrFieldVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Place = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Purpose = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrFieldVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrFieldVisits_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPolicyAcks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPolicyAcks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPolicyAcks_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrPolicyAcks_HrPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "HrPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrSalesKpis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Vertical = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Period = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Scope = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Achievement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncentiveAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSalesKpis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrSalesKpis_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrSiteAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Vertical = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Project = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Site = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Role = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ReportingManager = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSiteAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrSiteAllocations_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTerritoryMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Territory = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Area = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Distributor = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Market = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTerritoryMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTerritoryMaps_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    DefaultItemType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
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
                    table.PrimaryKey("PK_PropCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropCategories_PropCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "PropCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropStores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    KeeperId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_PropStores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropStores_Users_KeeperId",
                        column: x => x.KeeperId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PropIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    QuotationId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    EventName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EventType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ClientName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VenueName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VenueAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DispatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualReturnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StoreId = table.Column<int>(type: "integer", nullable: true),
                    VehicleNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DriverName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DriverPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SiteInChargeId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DamageRecovery = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PropIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropIssues_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropIssues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropIssues_PropStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PropStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropIssues_Users_SiteInChargeId",
                        column: x => x.SiteInChargeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PropItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    StoreId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ItemType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Ownership = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Size = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Colour = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Material = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Unit = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Tags = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GoodQuantity = table.Column<int>(type: "integer", nullable: false),
                    RepairableQuantity = table.Column<int>(type: "integer", nullable: false),
                    DamagedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    RentalRatePerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReplacementValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SupplierName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PackingUnit = table.Column<int>(type: "integer", nullable: true),
                    IsFragile = table.Column<bool>(type: "boolean", nullable: false),
                    IsSerialised = table.Column<bool>(type: "boolean", nullable: false),
                    TurnaroundDays = table.Column<int>(type: "integer", nullable: false),
                    StorageLocation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
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
                    table.PrimaryKey("PK_PropItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropItems_PropCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PropCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropItems_PropStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PropStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropItems_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PropIssueLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropIssueId = table.Column<int>(type: "integer", nullable: false),
                    PropItemId = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
                    IssuedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "integer", nullable: false),
                    DamagedQuantity = table.Column<int>(type: "integer", nullable: false),
                    LostQuantity = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RatePerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ChargeableDays = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropIssueLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropIssueLines_PropIssues_PropIssueId",
                        column: x => x.PropIssueId,
                        principalTable: "PropIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropIssueLines_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropItemPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropItemId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Caption = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropItemPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropItemPhotos_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PropItemId = table.Column<int>(type: "integer", nullable: false),
                    PropIssueId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    QuotationId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ClientName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropReservations_PropIssues_PropIssueId",
                        column: x => x.PropIssueId,
                        principalTable: "PropIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropReservations_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropStockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PropItemId = table.Column<int>(type: "integer", nullable: false),
                    MovementType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    FromCondition = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToCondition = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    PropIssueId = table.Column<int>(type: "integer", nullable: true),
                    StoreId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    HandledBy = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    MovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropStockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropStockMovements_PropIssues_PropIssueId",
                        column: x => x.PropIssueId,
                        principalTable: "PropIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropStockMovements_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrFieldVisits_EmployeeId",
                table: "HrFieldVisits",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicyAcks_EmployeeId",
                table: "HrPolicyAcks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPolicyAcks_PolicyId_EmployeeId",
                table: "HrPolicyAcks",
                columns: new[] { "PolicyId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrSalesKpis_EmployeeId",
                table: "HrSalesKpis",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrSiteAllocations_EmployeeId",
                table: "HrSiteAllocations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTerritoryMaps_EmployeeId",
                table: "HrTerritoryMaps",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropCategories_CompanyId_Code",
                table: "PropCategories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropCategories_CompanyId_SortOrder",
                table: "PropCategories",
                columns: new[] { "CompanyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PropCategories_ParentId",
                table: "PropCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PropIssueLines_PropIssueId_PropItemId",
                table: "PropIssueLines",
                columns: new[] { "PropIssueId", "PropItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropIssueLines_PropItemId",
                table: "PropIssueLines",
                column: "PropItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_CompanyId_Code",
                table: "PropIssues",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_CompanyId_DispatchDate",
                table: "PropIssues",
                columns: new[] { "CompanyId", "DispatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_CompanyId_ExpectedReturnDate",
                table: "PropIssues",
                columns: new[] { "CompanyId", "ExpectedReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_CompanyId_LeadId",
                table: "PropIssues",
                columns: new[] { "CompanyId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_CompanyId_Status",
                table: "PropIssues",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_LeadId",
                table: "PropIssues",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_ProjectId",
                table: "PropIssues",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_SiteInChargeId",
                table: "PropIssues",
                column: "SiteInChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropIssues_StoreId",
                table: "PropIssues",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PropItemPhotos_PropItemId_SortOrder",
                table: "PropItemPhotos",
                columns: new[] { "PropItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_CategoryId",
                table: "PropItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_CompanyId_CategoryId_Status",
                table: "PropItems",
                columns: new[] { "CompanyId", "CategoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_CompanyId_Code",
                table: "PropItems",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_CompanyId_ItemType",
                table: "PropItems",
                columns: new[] { "CompanyId", "ItemType" });

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_CompanyId_StoreId",
                table: "PropItems",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_OwnerId",
                table: "PropItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PropItems_StoreId",
                table: "PropItems",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PropReservations_CompanyId_LeadId",
                table: "PropReservations",
                columns: new[] { "CompanyId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropReservations_CompanyId_Status_FromDate",
                table: "PropReservations",
                columns: new[] { "CompanyId", "Status", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropReservations_PropIssueId",
                table: "PropReservations",
                column: "PropIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_PropReservations_PropItemId_FromDate_ToDate",
                table: "PropReservations",
                columns: new[] { "PropItemId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PropStockMovements_CompanyId_MovedAt",
                table: "PropStockMovements",
                columns: new[] { "CompanyId", "MovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropStockMovements_CompanyId_MovementType",
                table: "PropStockMovements",
                columns: new[] { "CompanyId", "MovementType" });

            migrationBuilder.CreateIndex(
                name: "IX_PropStockMovements_PropIssueId",
                table: "PropStockMovements",
                column: "PropIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_PropStockMovements_PropItemId_MovedAt",
                table: "PropStockMovements",
                columns: new[] { "PropItemId", "MovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PropStores_CompanyId_Code",
                table: "PropStores",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropStores_KeeperId",
                table: "PropStores",
                column: "KeeperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrFieldVisits");

            migrationBuilder.DropTable(
                name: "HrPolicyAcks");

            migrationBuilder.DropTable(
                name: "HrSalesKpis");

            migrationBuilder.DropTable(
                name: "HrSiteAllocations");

            migrationBuilder.DropTable(
                name: "HrTerritoryMaps");

            migrationBuilder.DropTable(
                name: "PropIssueLines");

            migrationBuilder.DropTable(
                name: "PropItemPhotos");

            migrationBuilder.DropTable(
                name: "PropReservations");

            migrationBuilder.DropTable(
                name: "PropStockMovements");

            migrationBuilder.DropTable(
                name: "PropIssues");

            migrationBuilder.DropTable(
                name: "PropItems");

            migrationBuilder.DropTable(
                name: "PropCategories");

            migrationBuilder.DropTable(
                name: "PropStores");
        }
    }
}
