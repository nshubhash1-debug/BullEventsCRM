using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventsAndWeddingDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolder paired the retiring property columns with the new
            // event ones by position and emitted renames — RequirementType to
            // ServicesNeeded, FundingMode to PartnerName, and four more. Those
            // pairs share nothing but an ordinal: a rename would have carried
            // "HomeLoan" into a partner's name and "2BHK" into a meal
            // preference. They are dropped and the event columns added empty.
            migrationBuilder.DropColumn(name: "RequirementType", table: "Leads");
            migrationBuilder.DropColumn(name: "ProductGroup", table: "Leads");
            migrationBuilder.DropColumn(name: "PossessionTimelineMonths", table: "Leads");
            migrationBuilder.DropColumn(name: "FundingMode", table: "Leads");
            migrationBuilder.DropColumn(name: "Configuration", table: "Leads");
            migrationBuilder.DropColumn(name: "AreaRange", table: "Leads");

            migrationBuilder.AddColumn<string>(
                name: "ServicesNeeded",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMode",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerName",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "MealPreference",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Functions",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "FloatingCapacity",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasAttachedKitchen",
                table: "Units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasStage",
                table: "Units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAirConditioned",
                table: "Units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutdoor",
                table: "Units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPlates",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PeakDatePremium",
                table: "Units",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerPlate",
                table: "Units",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SeatingCapacity",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDeposit",
                table: "Units",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TheatreCapacity",
                table: "Units",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsAlcohol",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsOpenFlame",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsOutsideCatering",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GuestRooms",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "NoiseCurfew",
                table: "Projects",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingCapacity",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventCategory",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "EventDate",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EventEndDate",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventSlot",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDateFlexible",
                table: "Leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "India Standard Time",
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldCollation: "crm_ci");

            migrationBuilder.AlterColumn<int>(
                name: "FinancialYearStartMonth",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 4,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR",
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldCollation: "crm_ci");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                defaultValue: "India",
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldCollation: "crm_ci");

            migrationBuilder.CreateTable(
                name: "SpaceBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    GroupRef = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    HoldExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    QuotationId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EventType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GuestCount = table.Column<int>(type: "integer", nullable: true),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaceBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpaceBookings_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SpaceBookings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpaceBookings_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceBookings_CompanyId_EventDate",
                table: "SpaceBookings",
                columns: new[] { "CompanyId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceBookings_CompanyId_GroupRef",
                table: "SpaceBookings",
                columns: new[] { "CompanyId", "GroupRef" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceBookings_LeadId",
                table: "SpaceBookings",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceBookings_ProjectId_EventDate",
                table: "SpaceBookings",
                columns: new[] { "ProjectId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceBookings_UnitId_EventDate",
                table: "SpaceBookings",
                columns: new[] { "UnitId", "EventDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpaceBookings");

            migrationBuilder.DropColumn(
                name: "FloatingCapacity",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "HasAttachedKitchen",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "HasStage",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsAirConditioned",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsOutdoor",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "MinimumPlates",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PeakDatePremium",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PricePerPlate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "SeatingCapacity",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "SecurityDeposit",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "TheatreCapacity",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "AllowsAlcohol",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AllowsOpenFlame",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AllowsOutsideCatering",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GuestRooms",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "NoiseCurfew",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ParkingCapacity",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EventCategory",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "EventDate",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "EventEndDate",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "EventSlot",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "IsDateFlexible",
                table: "Leads");

            // Mirrors the Up: the event columns are dropped and the property
            // ones recreated empty, rather than renamed across. Rolling back
            // discards the event data — there is nowhere in the old schema for
            // a guest count or an event date to go.
            migrationBuilder.DropColumn(name: "ServicesNeeded", table: "Leads");
            migrationBuilder.DropColumn(name: "PaymentMode", table: "Leads");
            migrationBuilder.DropColumn(name: "PartnerName", table: "Leads");
            migrationBuilder.DropColumn(name: "MealPreference", table: "Leads");
            migrationBuilder.DropColumn(name: "GuestCount", table: "Leads");
            migrationBuilder.DropColumn(name: "Functions", table: "Leads");

            migrationBuilder.AddColumn<string>(
                name: "RequirementType",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "ProductGroup",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "FundingMode",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Configuration",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "PossessionTimelineMonths",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaRange",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "India Standard Time",
                oldCollation: "crm_ci");

            migrationBuilder.AlterColumn<int>(
                name: "FinancialYearStartMonth",
                table: "Companies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 4);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldDefaultValue: "INR",
                oldCollation: "crm_ci");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                collation: "crm_ci",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldDefaultValue: "India",
                oldCollation: "crm_ci");
        }
    }
}
