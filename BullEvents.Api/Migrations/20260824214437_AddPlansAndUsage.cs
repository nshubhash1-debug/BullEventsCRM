using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchLimitOverride",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntitlementNote",
                table: "Companies",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "LeadLimitOverride",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewsAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatLimitOverride",
                table: "Companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UsageSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlanTier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "crm_ci"),
                    ActiveUsers = table.Column<int>(type: "integer", nullable: false),
                    Leads = table.Column<int>(type: "integer", nullable: false),
                    Branches = table.Column<int>(type: "integer", nullable: false),
                    Quotations = table.Column<int>(type: "integer", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    LeadsCreated = table.Column<int>(type: "integer", nullable: false),
                    SignIns = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageSnapshots_CompanyId_Day",
                table: "UsageSnapshots",
                columns: new[] { "CompanyId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageSnapshots");

            migrationBuilder.DropColumn(
                name: "BranchLimitOverride",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EntitlementNote",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LeadLimitOverride",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RenewsAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SeatLimitOverride",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Companies");
        }
    }
}
