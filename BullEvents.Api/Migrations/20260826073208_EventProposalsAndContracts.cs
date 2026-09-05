using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventProposalsAndContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EventDate",
                table: "Quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EventEndDate",
                table: "Quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventSlot",
                table: "Quotations",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "Quotations",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Functions",
                table: "Quotations",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPlates",
                table: "Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // "FromBooking", not the scaffolder's empty string: every milestone
            // that already exists was written against a booking date, and an
            // empty anchor is a value MilestoneAnchors.All does not contain —
            // readable, but rejected the moment anything validates it.
            migrationBuilder.AddColumn<string>(
                name: "DueAnchor",
                table: "PaymentPlanMilestones",
                type: "text",
                nullable: false,
                defaultValue: "FromBooking",
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "EventDate",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EventEndDate",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventSlot",
                table: "Bookings",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "Bookings",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "FinalGuestCount",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Functions",
                table: "Bookings",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPlates",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventDate",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "EventEndDate",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "EventSlot",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "Functions",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "MinimumPlates",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DueAnchor",
                table: "PaymentPlanMilestones");

            migrationBuilder.DropColumn(
                name: "EventDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EventEndDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EventSlot",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FinalGuestCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Functions",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MinimumPlates",
                table: "Bookings");
        }
    }
}
