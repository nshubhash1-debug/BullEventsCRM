using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class PayslipRunForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move the link before dropping the column that held it.
            //
            // RunId was a shadow foreign key EF invented because the navigation
            // is called Run and no property called RunId existed. Every payslip
            // ever written was linked through it, while the PayrollRunId column
            // the code queries on stayed at zero. Backfill first, or the foreign
            // key added at the end of this migration rejects every existing row.
            migrationBuilder.Sql("""
                UPDATE "HrPayslips" SET "PayrollRunId" = "RunId"
                WHERE "RunId" IS NOT NULL AND "PayrollRunId" = 0;
                """);

            // A slip with neither link belongs to no run and cannot be shown on
            // one. There is nothing to preserve and it would fail the new key.
            migrationBuilder.Sql("""
                DELETE FROM "HrPayslips" WHERE "RunId" IS NULL AND "PayrollRunId" = 0;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_HrPayslips_HrPayrollRuns_RunId",
                table: "HrPayslips");

            migrationBuilder.DropIndex(
                name: "IX_HrPayslips_RunId",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "HrPayslips");

            migrationBuilder.AddForeignKey(
                name: "FK_HrPayslips_HrPayrollRuns_PayrollRunId",
                table: "HrPayslips",
                column: "PayrollRunId",
                principalTable: "HrPayrollRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HrPayslips_HrPayrollRuns_PayrollRunId",
                table: "HrPayslips");

            migrationBuilder.AddColumn<int>(
                name: "RunId",
                table: "HrPayslips",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrPayslips_RunId",
                table: "HrPayslips",
                column: "RunId");

            migrationBuilder.AddForeignKey(
                name: "FK_HrPayslips_HrPayrollRuns_RunId",
                table: "HrPayslips",
                column: "RunId",
                principalTable: "HrPayrollRuns",
                principalColumn: "Id");
        }
    }
}
