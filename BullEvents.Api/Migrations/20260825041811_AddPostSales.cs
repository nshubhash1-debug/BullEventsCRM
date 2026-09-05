using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPostSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    BookingNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    QuotationId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    TowerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, collation: "crm_ci"),
                    UnitNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "crm_ci"),
                    Configuration = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SaleableArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AreaUnit = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AgreementValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentPlanId = table.Column<int>(type: "integer", nullable: true),
                    PaymentPlanName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Demanded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Received = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestCharged = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestWaived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Outstanding = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "crm_ci"),
                    BookingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BookingAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    AllotmentLetterOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DraftSharedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FrankedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegisteredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsiderationValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StampDuty = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RegistrationFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidByCustomer = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    SubRegistrarOffice = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingAgreements_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingApplicants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Salutation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    Relation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Pan = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, collation: "crm_ci"),
                    AadhaarLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true, collation: "crm_ci"),
                    KycStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    KycVerifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingApplicants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingApplicants_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingCancellations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AmountReceived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BrokerageRecovered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    RefundedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundReference = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingCancellations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingCancellations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    Stage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicantId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedById = table.Column<int>(type: "integer", nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "crm_ci"),
                    FileName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingDocuments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    Percent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BasicAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConstructionStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    DemandId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingMilestones_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    ToName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    ToPhone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToEmail = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToPan = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TransferChargePercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransferChargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransferChargeReceived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingTransfers_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Brokerages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    PartnerUserId = table.Column<int>(type: "integer", nullable: true),
                    PartnerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    PartnerFirm = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsWithheld = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecoveredAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brokerages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Brokerages_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Demands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    BookingMilestoneId = table.Column<int>(type: "integer", nullable: true),
                    DemandNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    RaisedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BasicAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Received = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAccrued = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestWaived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    CancelledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Demands_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeLoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, collation: "crm_ci"),
                    BranchName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ApplicationNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    AppliedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SanctionedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SanctionedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SanctionValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TripartiteSignedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisbursedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    RejectionReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeLoans_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Possessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    CommittedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfferedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnagsRaised = table.Column<int>(type: "integer", nullable: false),
                    SnagsClosed = table.Column<int>(type: "integer", nullable: false),
                    FitOutFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FitOutTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaintenanceAdvanceMonths = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaintenanceCollected = table.Column<bool>(type: "boolean", nullable: false),
                    CorpusDeposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CorpusCollected = table.Column<bool>(type: "boolean", nullable: false),
                    DuesCleared = table.Column<bool>(type: "boolean", nullable: false),
                    DocumentsHandedOver = table.Column<bool>(type: "boolean", nullable: false),
                    HandedOverOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Possessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Possessions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Mode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    Instrument = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, collation: "crm_ci"),
                    InstrumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    ClearedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BouncedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BounceReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Unallocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BrokerageSlabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BrokerageId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    SharePercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    CollectionThresholdPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsEarned = table.Column<bool>(type: "boolean", nullable: false),
                    EarnedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerageSlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrokerageSlabs_Brokerages_BrokerageId",
                        column: x => x.BrokerageId,
                        principalTable: "Brokerages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EscrowEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: false),
                    On = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiptAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DesignatedPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DesignatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Transferred = table.Column<bool>(type: "boolean", nullable: false),
                    TransferredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransferReference = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscrowEntries_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: false),
                    DemandId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TowardsInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptAllocations_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptAllocations_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAgreements_BookingId",
                table: "BookingAgreements",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingApplicants_BookingId",
                table: "BookingApplicants",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingCancellations_BookingId",
                table: "BookingCancellations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDocuments_BookingId_Stage",
                table: "BookingDocuments",
                columns: new[] { "BookingId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingMilestones_BookingId",
                table: "BookingMilestones",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingMilestones_CompanyId_ConstructionStage_Status",
                table: "BookingMilestones",
                columns: new[] { "CompanyId", "ConstructionStage", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BranchId",
                table: "Bookings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_BookingNumber",
                table: "Bookings",
                columns: new[] { "CompanyId", "BookingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_Status_OverdueDays",
                table: "Bookings",
                columns: new[] { "CompanyId", "Status", "OverdueDays" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerId",
                table: "Bookings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UnitId",
                table: "Bookings",
                column: "UnitId",
                unique: true,
                filter: "\"Status\" <> 'Cancelled' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTransfers_BookingId",
                table: "BookingTransfers",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Brokerages_BookingId",
                table: "Brokerages",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerageSlabs_BrokerageId",
                table: "BrokerageSlabs",
                column: "BrokerageId");

            migrationBuilder.CreateIndex(
                name: "IX_Demands_BookingId",
                table: "Demands",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Demands_CompanyId_DemandNumber",
                table: "Demands",
                columns: new[] { "CompanyId", "DemandNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Demands_CompanyId_Status_DueDate",
                table: "Demands",
                columns: new[] { "CompanyId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowEntries_CompanyId_ProjectId_On",
                table: "EscrowEntries",
                columns: new[] { "CompanyId", "ProjectId", "On" });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowEntries_ReceiptId",
                table: "EscrowEntries",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeLoans_BookingId",
                table: "HomeLoans",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Possessions_BookingId",
                table: "Possessions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_DemandId",
                table: "ReceiptAllocations",
                column: "DemandId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_ReceiptId_DemandId",
                table: "ReceiptAllocations",
                columns: new[] { "ReceiptId", "DemandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_BookingId",
                table: "Receipts",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CompanyId_ReceiptNumber",
                table: "Receipts",
                columns: new[] { "CompanyId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CompanyId_ReceivedOn",
                table: "Receipts",
                columns: new[] { "CompanyId", "ReceivedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingAgreements");

            migrationBuilder.DropTable(
                name: "BookingApplicants");

            migrationBuilder.DropTable(
                name: "BookingCancellations");

            migrationBuilder.DropTable(
                name: "BookingDocuments");

            migrationBuilder.DropTable(
                name: "BookingMilestones");

            migrationBuilder.DropTable(
                name: "BookingTransfers");

            migrationBuilder.DropTable(
                name: "BrokerageSlabs");

            migrationBuilder.DropTable(
                name: "EscrowEntries");

            migrationBuilder.DropTable(
                name: "HomeLoans");

            migrationBuilder.DropTable(
                name: "Possessions");

            migrationBuilder.DropTable(
                name: "ReceiptAllocations");

            migrationBuilder.DropTable(
                name: "Brokerages");

            migrationBuilder.DropTable(
                name: "Demands");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
