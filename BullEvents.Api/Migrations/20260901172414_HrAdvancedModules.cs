using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class HrAdvancedModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HrAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Body = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PinUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Audience = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAnnouncements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrAppraisalCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAppraisalCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrExpenseClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    BillUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrExpenseClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrExpenseClaims_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrHelpdeskTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Category = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Priority = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Body = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AssignedToEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrHelpdeskTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrHelpdeskTickets_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Optional = table.Column<bool>(type: "boolean", nullable: false),
                    Locations = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrInterviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Panel = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Mode = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Score = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Recommendation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrInterviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrInterviews_HrCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "HrCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FromValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ToValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLifecycleEvents_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Category = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Body = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrPunches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Device = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPunches_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTrainings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Trainer = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Venue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTrainings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrAppraisals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CycleId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    SelfScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ManagerScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Rating = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Comments = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAppraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAppraisals_HrAppraisalCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "HrAppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrAppraisals_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CycleId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Kra = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Target = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Weight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Progress = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrGoals_HrAppraisalCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "HrAppraisalCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HrGoals_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTrainingEnrolments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TrainingId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Score = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTrainingEnrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTrainingEnrolments_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrTrainingEnrolments_HrTrainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "HrTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrAppraisals_CycleId_EmployeeId",
                table: "HrAppraisals",
                columns: new[] { "CycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrAppraisals_EmployeeId",
                table: "HrAppraisals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExpenseClaims_EmployeeId",
                table: "HrExpenseClaims",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrGoals_CycleId",
                table: "HrGoals",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_HrGoals_EmployeeId",
                table: "HrGoals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrHelpdeskTickets_EmployeeId",
                table: "HrHelpdeskTickets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrHolidays_CompanyId_OnDate_Name",
                table: "HrHolidays",
                columns: new[] { "CompanyId", "OnDate", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviews_CandidateId",
                table: "HrInterviews",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLifecycleEvents_EmployeeId",
                table: "HrLifecycleEvents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPunches_EmployeeId_At",
                table: "HrPunches",
                columns: new[] { "EmployeeId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_HrTrainingEnrolments_EmployeeId",
                table: "HrTrainingEnrolments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTrainingEnrolments_TrainingId_EmployeeId",
                table: "HrTrainingEnrolments",
                columns: new[] { "TrainingId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrAnnouncements");

            migrationBuilder.DropTable(
                name: "HrAppraisals");

            migrationBuilder.DropTable(
                name: "HrExpenseClaims");

            migrationBuilder.DropTable(
                name: "HrGoals");

            migrationBuilder.DropTable(
                name: "HrHelpdeskTickets");

            migrationBuilder.DropTable(
                name: "HrHolidays");

            migrationBuilder.DropTable(
                name: "HrInterviews");

            migrationBuilder.DropTable(
                name: "HrLifecycleEvents");

            migrationBuilder.DropTable(
                name: "HrPolicies");

            migrationBuilder.DropTable(
                name: "HrPunches");

            migrationBuilder.DropTable(
                name: "HrTrainingEnrolments");

            migrationBuilder.DropTable(
                name: "HrAppraisalCycles");

            migrationBuilder.DropTable(
                name: "HrTrainings");
        }
    }
}
