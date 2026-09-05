using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class QuotationResourcePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuotationResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    QuotationId = table.Column<int>(type: "integer", nullable: false),
                    ResourceKind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    State = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PropItemId = table.Column<int>(type: "integer", nullable: true),
                    PropKitId = table.Column<int>(type: "integer", nullable: true),
                    CrewMemberId = table.Column<int>(type: "integer", nullable: true),
                    CrewRole = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    VendorRateId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ChargeGroup = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsInternalOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityUnit = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitSell = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PropReservationId = table.Column<int>(type: "integer", nullable: true),
                    PropIssueId = table.Column<int>(type: "integer", nullable: true),
                    CrewAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    VendorPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationResources_CrewMembers_CrewMemberId",
                        column: x => x.CrewMemberId,
                        principalTable: "CrewMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuotationResources_PropItems_PropItemId",
                        column: x => x.PropItemId,
                        principalTable: "PropItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuotationResources_PropKits_PropKitId",
                        column: x => x.PropKitId,
                        principalTable: "PropKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuotationResources_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuotationResources_VendorRates_VendorRateId",
                        column: x => x.VendorRateId,
                        principalTable: "VendorRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuotationResources_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_CompanyId_State",
                table: "QuotationResources",
                columns: new[] { "CompanyId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_CrewMemberId",
                table: "QuotationResources",
                column: "CrewMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_PropItemId",
                table: "QuotationResources",
                column: "PropItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_PropKitId",
                table: "QuotationResources",
                column: "PropKitId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_QuotationId_SortOrder",
                table: "QuotationResources",
                columns: new[] { "QuotationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_VendorId",
                table: "QuotationResources",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationResources_VendorRateId",
                table: "QuotationResources",
                column: "VendorRateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationResources");
        }
    }
}
