using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class StatutoryBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "crm_ci"),
                    TradeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "crm_ci"),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, collation: "crm_ci"),
                    Pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, collation: "crm_ci"),
                    StateName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "crm_ci"),
                    StateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, collation: "crm_ci"),
                    RegisteredAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DefaultTreatment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "crm_ci"),
                    DefaultSacCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, collation: "crm_ci"),
                    OccupancyCertificateOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoicePrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    CreditNotePrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    BankAccountName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BankAccountNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BankIfsc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true, collation: "crm_ci"),
                    BankBranch = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GstProfiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostDatedCheques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    DemandId = table.Column<int>(type: "integer", nullable: true),
                    ChequeNumber = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    BranchName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, collation: "crm_ci"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChequeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DepositedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClearedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BouncedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BounceReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostDatedCheques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostDatedCheques_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    DemandId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Treatment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "crm_ci"),
                    SacCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, collation: "crm_ci"),
                    GrossValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LandAbatement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GstRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "crm_ci"),
                    CustomerGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true, collation: "crm_ci"),
                    CustomerAddress = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CustomerPan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, collation: "crm_ci"),
                    PlaceOfSupply = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxInvoices_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaxInvoices_Demands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "Demands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TdsCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<int>(type: "integer", nullable: false),
                    DeductorPan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, collation: "crm_ci"),
                    DeductorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "crm_ci"),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TdsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quarter = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, collation: "crm_ci"),
                    CertificateNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true, collation: "crm_ci"),
                    CertificateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChallanNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true, collation: "crm_ci"),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TdsCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TdsCertificates_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TdsCertificates_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    TaxInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    CreditNoteNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "crm_ci"),
                    Narrative = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GrossValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GstRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditNotes_TaxInvoices_TaxInvoiceId",
                        column: x => x.TaxInvoiceId,
                        principalTable: "TaxInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_BookingId",
                table: "CreditNotes",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CompanyId_CreditNoteNumber",
                table: "CreditNotes",
                columns: new[] { "CompanyId", "CreditNoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_TaxInvoiceId",
                table: "CreditNotes",
                column: "TaxInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_GstProfiles_CompanyId_ProjectId",
                table: "GstProfiles",
                columns: new[] { "CompanyId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GstProfiles_ProjectId",
                table: "GstProfiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PostDatedCheques_BookingId",
                table: "PostDatedCheques",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PostDatedCheques_CompanyId_Status_ChequeDate",
                table: "PostDatedCheques",
                columns: new[] { "CompanyId", "Status", "ChequeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_BookingId",
                table: "TaxInvoices",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_CompanyId_InvoiceNumber",
                table: "TaxInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_CompanyId_Status_InvoiceDate",
                table: "TaxInvoices",
                columns: new[] { "CompanyId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_DemandId",
                table: "TaxInvoices",
                column: "DemandId");

            migrationBuilder.CreateIndex(
                name: "IX_TdsCertificates_BookingId",
                table: "TdsCertificates",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TdsCertificates_CompanyId_Status_Quarter",
                table: "TdsCertificates",
                columns: new[] { "CompanyId", "Status", "Quarter" });

            migrationBuilder.CreateIndex(
                name: "IX_TdsCertificates_ReceiptId",
                table: "TdsCertificates",
                column: "ReceiptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropTable(
                name: "GstProfiles");

            migrationBuilder.DropTable(
                name: "PostDatedCheques");

            migrationBuilder.DropTable(
                name: "TdsCertificates");

            migrationBuilder.DropTable(
                name: "TaxInvoices");
        }
    }
}
