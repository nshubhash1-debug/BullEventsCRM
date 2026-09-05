using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullRealty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnniversaryDate",
                table: "Leads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaRange",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Leads",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FatherOrSpouseName",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "InterestedProjectId",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductGroup",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SubStatus",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "Leads",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_InterestedProjectId",
                table: "Leads",
                column: "InterestedProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Projects_InterestedProjectId",
                table: "Leads",
                column: "InterestedProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Projects_InterestedProjectId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_InterestedProjectId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AnniversaryDate",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "AreaRange",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "FatherOrSpouseName",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "InterestedProjectId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ProductGroup",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SubStatus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "Leads");
        }
    }
}
