using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullRealty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationPropertyLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ProjectId",
                table: "Quotations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_UnitId",
                table: "Quotations",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Projects_ProjectId",
                table: "Quotations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Units_UnitId",
                table: "Quotations",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Projects_ProjectId",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Units_UnitId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_ProjectId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_UnitId",
                table: "Quotations");
        }
    }
}
