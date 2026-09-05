using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class SalaryComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrEmployeeAdvances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Instalments = table.Column<int>(type: "integer", nullable: false),
                    InstalmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecoveryStartYear = table.Column<int>(type: "integer", nullable: false),
                    RecoveryStartMonth = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    WriteOffReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEmployeeAdvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrEmployeeAdvances_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPayStructures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPayStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrSalaryComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Abbreviation = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ComponentType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Calculation = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Formula = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AffectsPf = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsEsi = table.Column<bool>(type: "boolean", nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    IsHra = table.Column<bool>(type: "boolean", nullable: false),
                    DependsOnPaymentDays = table.Column<bool>(type: "boolean", nullable: false),
                    IsStatutory = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSalaryComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrAdvanceRepayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeAdvanceId = table.Column<int>(type: "integer", nullable: false),
                    PayrollRunId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAdvanceRepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAdvanceRepayments_HrEmployeeAdvances_EmployeeAdvanceId",
                        column: x => x.EmployeeAdvanceId,
                        principalTable: "HrEmployeeAdvances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrAdvanceRepayments_HrPayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "HrPayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPayStructureAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    PayStructureId = table.Column<int>(type: "integer", nullable: false),
                    Base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPayStructureAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPayStructureAssignments_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrPayStructureAssignments_HrPayStructures_PayStructureId",
                        column: x => x.PayStructureId,
                        principalTable: "HrPayStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrAdditionalSalaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    RecurringUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DependsOnPaymentDays = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PaidInPayrollRunId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAdditionalSalaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAdditionalSalaries_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrAdditionalSalaries_HrSalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "HrSalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPayslipLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PayslipId = table.Column<int>(type: "integer", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Abbreviation = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ComponentType = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsStatutory = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPayslipLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPayslipLines_HrPayslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "HrPayslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrPayslipLines_HrSalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "HrSalaryComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HrPayStructureLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PayStructureId = table.Column<int>(type: "integer", nullable: false),
                    SalaryComponentId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Formula = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPayStructureLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPayStructureLines_HrPayStructures_PayStructureId",
                        column: x => x.PayStructureId,
                        principalTable: "HrPayStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrPayStructureLines_HrSalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "HrSalaryComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrAdditionalSalaries_EmployeeId_Year_Month",
                table: "HrAdditionalSalaries",
                columns: new[] { "EmployeeId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_HrAdditionalSalaries_SalaryComponentId",
                table: "HrAdditionalSalaries",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAdvanceRepayments_EmployeeAdvanceId_PayrollRunId",
                table: "HrAdvanceRepayments",
                columns: new[] { "EmployeeAdvanceId", "PayrollRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrAdvanceRepayments_PayrollRunId",
                table: "HrAdvanceRepayments",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeAdvances_EmployeeId_Status",
                table: "HrEmployeeAdvances",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrPayslipLines_PayslipId",
                table: "HrPayslipLines",
                column: "PayslipId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPayslipLines_SalaryComponentId",
                table: "HrPayslipLines",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPayStructureAssignments_EmployeeId_EffectiveFrom",
                table: "HrPayStructureAssignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_HrPayStructureAssignments_PayStructureId",
                table: "HrPayStructureAssignments",
                column: "PayStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPayStructureLines_PayStructureId_SalaryComponentId",
                table: "HrPayStructureLines",
                columns: new[] { "PayStructureId", "SalaryComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrPayStructureLines_SalaryComponentId",
                table: "HrPayStructureLines",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrSalaryComponents_CompanyId_Abbreviation",
                table: "HrSalaryComponents",
                columns: new[] { "CompanyId", "Abbreviation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrAdditionalSalaries");

            migrationBuilder.DropTable(
                name: "HrAdvanceRepayments");

            migrationBuilder.DropTable(
                name: "HrPayslipLines");

            migrationBuilder.DropTable(
                name: "HrPayStructureAssignments");

            migrationBuilder.DropTable(
                name: "HrPayStructureLines");

            migrationBuilder.DropTable(
                name: "HrEmployeeAdvances");

            migrationBuilder.DropTable(
                name: "HrPayStructures");

            migrationBuilder.DropTable(
                name: "HrSalaryComponents");
        }
    }
}
