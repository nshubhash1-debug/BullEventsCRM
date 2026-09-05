using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullRealty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadSupportingManagerAndObmLeadLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "ObmVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupportingManagerId",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_LeadId",
                table: "SiteVisits",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ObmVisits_LeadId",
                table: "ObmVisits",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SupportingManagerId",
                table: "Leads",
                column: "SupportingManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Users_SupportingManagerId",
                table: "Leads",
                column: "SupportingManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Users_SupportingManagerId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_SiteVisits_LeadId",
                table: "SiteVisits");

            migrationBuilder.DropIndex(
                name: "IX_ObmVisits_LeadId",
                table: "ObmVisits");

            migrationBuilder.DropIndex(
                name: "IX_Leads_SupportingManagerId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "ObmVisits");

            migrationBuilder.DropColumn(
                name: "SupportingManagerId",
                table: "Leads");
        }
    }
}
