using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullRealty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitDurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "SiteVisits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ObmVisits",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "SiteVisits");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ObmVisits");
        }
    }
}
