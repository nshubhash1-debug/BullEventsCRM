using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class IndiaStatutoryPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostToCompany",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Edli",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EpfEmployer",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EpsEmployer",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GratuityAccrual",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HraExemption",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LwfEmployee",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LwfEmployer",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PayableGross",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PfAdminCharges",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PfWage",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfessionalTax",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProjectedAnnualTax",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProjectedAnnualTaxable",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegime",
                table: "HrPayslips",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "Tds",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeductions",
                table: "HrPayslips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "HrEmployeeTaxProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    FinancialYear = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AnnualRentPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RentsInMetro = table.Column<bool>(type: "boolean", nullable: false),
                    LandlordPan = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Section80C = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Section80Ccd1B = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Section80D = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HousingLoanInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Section80Tta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousEmployerSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousEmployerTds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProofsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    ProofsSubmittedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEmployeeTaxProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrEmployeeTaxProfiles_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrIncomeTaxSlabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    FinancialYear = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FromAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ToAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrIncomeTaxSlabs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrProfessionalTaxSlabs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FromAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ToAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrProfessionalTaxSlabs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrStatutoryConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PfEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PfEmployeeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfEmployerRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfWageCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfRestrictEmployeeToCeiling = table.Column<bool>(type: "boolean", nullable: false),
                    PfRestrictEmployerToCeiling = table.Column<bool>(type: "boolean", nullable: false),
                    EpsRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EpsWageCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EdliRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfAdminRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EsiEmployeeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiEmployerRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiWageThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PtEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PtDefaultState = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TdsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CessRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LwfEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LwfEmployeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LwfEmployerAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LwfMonths = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GratuityEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GratuityDaysPerYear = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GratuityMonthDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GratuityEligibleYears = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GratuityCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrStatutoryConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrTaxRegimeConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    FinancialYear = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    StandardDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RebateIncomeCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RebateMaximum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllowsChapterViaDeductions = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsHraExemption = table.Column<bool>(type: "boolean", nullable: false),
                    SurchargeBands = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTaxRegimeConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeTaxProfiles_EmployeeId_FinancialYear",
                table: "HrEmployeeTaxProfiles",
                columns: new[] { "EmployeeId", "FinancialYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrIncomeTaxSlabs_CompanyId_FinancialYear_Regime_SortOrder",
                table: "HrIncomeTaxSlabs",
                columns: new[] { "CompanyId", "FinancialYear", "Regime", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HrProfessionalTaxSlabs_CompanyId_IsActive",
                table: "HrProfessionalTaxSlabs",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HrProfessionalTaxSlabs_CompanyId_State_FromAmount",
                table: "HrProfessionalTaxSlabs",
                columns: new[] { "CompanyId", "State", "FromAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_HrStatutoryConfigs_CompanyId_EffectiveFrom",
                table: "HrStatutoryConfigs",
                columns: new[] { "CompanyId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_HrTaxRegimeConfigs_CompanyId_FinancialYear_Regime",
                table: "HrTaxRegimeConfigs",
                columns: new[] { "CompanyId", "FinancialYear", "Regime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrEmployeeTaxProfiles");

            migrationBuilder.DropTable(
                name: "HrIncomeTaxSlabs");

            migrationBuilder.DropTable(
                name: "HrProfessionalTaxSlabs");

            migrationBuilder.DropTable(
                name: "HrStatutoryConfigs");

            migrationBuilder.DropTable(
                name: "HrTaxRegimeConfigs");

            migrationBuilder.DropColumn(
                name: "CostToCompany",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "Edli",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "EpfEmployer",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "EpsEmployer",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "GratuityAccrual",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "HraExemption",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "LwfEmployee",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "LwfEmployer",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "PayableGross",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "PfAdminCharges",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "PfWage",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "ProfessionalTax",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "ProjectedAnnualTax",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "ProjectedAnnualTaxable",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "TaxRegime",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "Tds",
                table: "HrPayslips");

            migrationBuilder.DropColumn(
                name: "TotalDeductions",
                table: "HrPayslips");
        }
    }
}
