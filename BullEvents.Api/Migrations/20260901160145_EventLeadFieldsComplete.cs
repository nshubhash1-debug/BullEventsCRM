using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventLeadFieldsComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoAckAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CeremonyGuestCount",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CeremonyStyle",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConvertedBookingId",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InquirerRole",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "PartnerEmail",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "PartnerPhone",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "PlanningPackage",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "PortalName",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "QuestionnaireCompletedAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuestionnaireSentAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionnaireStatus",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "QuestionnaireToken",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "ReceptionGuestCount",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueStatus",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.Sql("""
                UPDATE "Leads" SET "Stage" = 'ProposalSent' WHERE "Stage" = 'Negotiation';
                UPDATE "Leads" SET "Stage" = 'Contacted' WHERE "Stage" = 'FollowUp';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Leads" SET "Stage" = 'Negotiation' WHERE "Stage" = 'ProposalSent';
                UPDATE "Leads" SET "Stage" = 'FollowUp' WHERE "Stage" = 'Contacted';
                """);

            migrationBuilder.DropColumn(
                name: "AutoAckAt",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CeremonyGuestCount",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CeremonyStyle",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConsultAt",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConvertedBookingId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "InquirerRole",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PartnerEmail",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PartnerPhone",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PlanningPackage",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PortalName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "QuestionnaireCompletedAt",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "QuestionnaireSentAt",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "QuestionnaireStatus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "QuestionnaireToken",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ReceptionGuestCount",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "VenueStatus",
                table: "Leads");
        }
    }
}
