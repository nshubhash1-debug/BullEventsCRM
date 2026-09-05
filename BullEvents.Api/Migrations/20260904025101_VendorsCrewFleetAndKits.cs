using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class VendorsCrewFleetAndKits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropKits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SetupType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EventType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RentalRatePerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SetupHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CrewRequired = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_PropKits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Services = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ContactPerson = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AltPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CoverageAreas = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Website = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GstNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PanNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BankAccountName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BankAccountNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BankIfsc = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PaymentTermDays = table.Column<int>(type: "integer", nullable: false),
                    AdvanceFraction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ConcurrentEventCapacity = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CompletedEvents = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
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
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vendors_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PropKitLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropKitId = table.Column<int>(type: "integer", nullable: false),
                    PropItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropKitLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropKitLines_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropKitLines_PropKits_PropKitId",
                        column: x => x.PropKitId,
                        principalTable: "PropKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrewMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EngagementType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    SupplierVendorId = table.Column<int>(type: "integer", nullable: true),
                    PrimaryRole = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SecondaryRoles = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    YearsExperience = table.Column<int>(type: "integer", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AltPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    City = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    WillTravel = table.Column<bool>(type: "boolean", nullable: false),
                    DayRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OvertimeHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Rating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EventsWorked = table.Column<int>(type: "integer", nullable: false),
                    NoShowCount = table.Column<int>(type: "integer", nullable: false),
                    IdProofType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IdProofNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
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
                    table.PrimaryKey("PK_CrewMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewMembers_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrewMembers_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrewMembers_Vendors_SupplierVendorId",
                        column: x => x.SupplierVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VendorDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FileName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Url = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorDocuments_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorPurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Service = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    QuotationId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    EventName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EventType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ClientName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VenueName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VenueAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GuestCount = table.Column<int>(type: "integer", nullable: true),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportingTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSell = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Terms = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CoordinatorId = table.Column<int>(type: "integer", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_VendorPurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPurchaseOrders_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorPurchaseOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorPurchaseOrders_Users_CoordinatorId",
                        column: x => x.CoordinatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorPurchaseOrders_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Basis = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SellRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorRates_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrewAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CrewMemberId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Role = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    PropIssueId = table.Column<int>(type: "integer", nullable: true),
                    EventName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ClientName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VenueName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportingTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ClosingTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DayRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OvertimeHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OvertimeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AllowanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewAssignments_CrewMembers_CrewMemberId",
                        column: x => x.CrewMemberId,
                        principalTable: "CrewMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrewAssignments_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrewAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    VehicleType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SupplierVendorId = table.Column<int>(type: "integer", nullable: true),
                    PayloadKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CapacityCubicFeet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PassengerSeats = table.Column<int>(type: "integer", nullable: true),
                    DefaultDriverCrewId = table.Column<int>(type: "integer", nullable: true),
                    DayRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RatePerKm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InsuranceExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    PermitExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    PucExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    FitnessExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    LastServicedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OdometerKm = table.Column<int>(type: "integer", nullable: true),
                    StoreId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
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
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_CrewMembers_DefaultDriverCrewId",
                        column: x => x.DefaultDriverCrewId,
                        principalTable: "CrewMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_PropStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PropStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_Vendors_SupplierVendorId",
                        column: x => x.SupplierVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VendorPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    VendorPurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Reference = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    TdsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPayments_VendorPurchaseOrders_VendorPurchaseOrderId",
                        column: x => x.VendorPurchaseOrderId,
                        principalTable: "VendorPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorPoLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorPurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    VendorRateId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Basis = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SellRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPoLines_VendorPurchaseOrders_VendorPurchaseOrderId",
                        column: x => x.VendorPurchaseOrderId,
                        principalTable: "VendorPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorPoLines_VendorRates_VendorRateId",
                        column: x => x.VendorRateId,
                        principalTable: "VendorRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VehicleTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Direction = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DriverCrewId = table.Column<int>(type: "integer", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartureTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    FromLocation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToLocation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    EventName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StartOdometerKm = table.Column<int>(type: "integer", nullable: true),
                    EndOdometerKm = table.Column<int>(type: "integer", nullable: true),
                    FuelCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TollCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LoadedWeightKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleTrips_CrewMembers_DriverCrewId",
                        column: x => x.DriverCrewId,
                        principalTable: "CrewMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleTrips_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleTrips_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleTrips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleTripLoads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleTripId = table.Column<int>(type: "integer", nullable: false),
                    PropIssueId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTripLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleTripLoads_PropIssues_PropIssueId",
                        column: x => x.PropIssueId,
                        principalTable: "PropIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleTripLoads_VehicleTrips_VehicleTripId",
                        column: x => x.VehicleTripId,
                        principalTable: "VehicleTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_CompanyId_FromDate",
                table: "CrewAssignments",
                columns: new[] { "CompanyId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_CompanyId_LeadId",
                table: "CrewAssignments",
                columns: new[] { "CompanyId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_CompanyId_Status_FromDate",
                table: "CrewAssignments",
                columns: new[] { "CompanyId", "Status", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_CrewMemberId_FromDate_ToDate",
                table: "CrewAssignments",
                columns: new[] { "CrewMemberId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_LeadId",
                table: "CrewAssignments",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewAssignments_ProjectId",
                table: "CrewAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_CompanyId_Code",
                table: "CrewMembers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_CompanyId_EngagementType",
                table: "CrewMembers",
                columns: new[] { "CompanyId", "EngagementType" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_CompanyId_PrimaryRole_Status",
                table: "CrewMembers",
                columns: new[] { "CompanyId", "PrimaryRole", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_EmployeeId",
                table: "CrewMembers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_OwnerId",
                table: "CrewMembers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_SupplierVendorId",
                table: "CrewMembers",
                column: "SupplierVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_PropKitLines_PropItemId",
                table: "PropKitLines",
                column: "PropItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PropKitLines_PropKitId_PropItemId",
                table: "PropKitLines",
                columns: new[] { "PropKitId", "PropItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropKits_CompanyId_Code",
                table: "PropKits",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropKits_CompanyId_IsActive",
                table: "PropKits",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_RegistrationNumber",
                table: "Vehicles",
                columns: new[] { "CompanyId", "RegistrationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_Status",
                table: "Vehicles",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DefaultDriverCrewId",
                table: "Vehicles",
                column: "DefaultDriverCrewId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_StoreId",
                table: "Vehicles",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_SupplierVendorId",
                table: "Vehicles",
                column: "SupplierVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTripLoads_PropIssueId",
                table: "VehicleTripLoads",
                column: "PropIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTripLoads_VehicleTripId_PropIssueId",
                table: "VehicleTripLoads",
                columns: new[] { "VehicleTripId", "PropIssueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_CompanyId_Code",
                table: "VehicleTrips",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_CompanyId_FromDate",
                table: "VehicleTrips",
                columns: new[] { "CompanyId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_CompanyId_Status_FromDate",
                table: "VehicleTrips",
                columns: new[] { "CompanyId", "Status", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_DriverCrewId",
                table: "VehicleTrips",
                column: "DriverCrewId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_LeadId",
                table: "VehicleTrips",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_ProjectId",
                table: "VehicleTrips",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrips_VehicleId_FromDate_ToDate",
                table: "VehicleTrips",
                columns: new[] { "VehicleId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorDocuments_CompanyId_ExpiryDate",
                table: "VendorDocuments",
                columns: new[] { "CompanyId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorDocuments_VendorId",
                table: "VendorDocuments",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_CompanyId_PaidOn",
                table: "VendorPayments",
                columns: new[] { "CompanyId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPayments_VendorPurchaseOrderId_PaidOn",
                table: "VendorPayments",
                columns: new[] { "VendorPurchaseOrderId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPoLines_VendorPurchaseOrderId_SortOrder",
                table: "VendorPoLines",
                columns: new[] { "VendorPurchaseOrderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPoLines_VendorRateId",
                table: "VendorPoLines",
                column: "VendorRateId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_CompanyId_Code",
                table: "VendorPurchaseOrders",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_CompanyId_LeadId",
                table: "VendorPurchaseOrders",
                columns: new[] { "CompanyId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_CompanyId_ServiceDate",
                table: "VendorPurchaseOrders",
                columns: new[] { "CompanyId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_CompanyId_Status",
                table: "VendorPurchaseOrders",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_CoordinatorId",
                table: "VendorPurchaseOrders",
                column: "CoordinatorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_LeadId",
                table: "VendorPurchaseOrders",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_ProjectId",
                table: "VendorPurchaseOrders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPurchaseOrders_VendorId_ServiceDate_ServiceEndDate",
                table: "VendorPurchaseOrders",
                columns: new[] { "VendorId", "ServiceDate", "ServiceEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorRates_VendorId_Service",
                table: "VendorRates",
                columns: new[] { "VendorId", "Service" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_City",
                table: "Vendors",
                columns: new[] { "CompanyId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_Code",
                table: "Vendors",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_Status",
                table: "Vendors",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_OwnerId",
                table: "Vendors",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrewAssignments");

            migrationBuilder.DropTable(
                name: "PropKitLines");

            migrationBuilder.DropTable(
                name: "VehicleTripLoads");

            migrationBuilder.DropTable(
                name: "VendorDocuments");

            migrationBuilder.DropTable(
                name: "VendorPayments");

            migrationBuilder.DropTable(
                name: "VendorPoLines");

            migrationBuilder.DropTable(
                name: "PropKits");

            migrationBuilder.DropTable(
                name: "VehicleTrips");

            migrationBuilder.DropTable(
                name: "VendorPurchaseOrders");

            migrationBuilder.DropTable(
                name: "VendorRates");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "CrewMembers");

            migrationBuilder.DropTable(
                name: "Vendors");
        }
    }
}
