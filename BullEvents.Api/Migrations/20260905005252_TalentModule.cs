using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class TalentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "HrTrainingEnrolments",
                type: "text",
                nullable: false,
                defaultValue: "",
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "TrainerRemarks",
                table: "HrTrainingEnrolments",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "InterviewRoundId",
                table: "HrInterviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppraisalTemplateId",
                table: "HrAppraisals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalScore",
                table: "HrAppraisals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HrAppraisalKras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AppraisalId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Weight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SelfScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ManagerScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SelfComment = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ManagerComment = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAppraisalKras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAppraisalKras_HrAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "HrAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrAppraisalTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAppraisalTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrEmployeeReferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ReferrerEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CandidateName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Phone = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Email = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Position = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CandidateId = table.Column<int>(type: "integer", nullable: true),
                    VacancyId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionMonths = table.Column<int>(type: "integer", nullable: false),
                    HiredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    BonusDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    BonusAdditionalSalaryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEmployeeReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrEmployeeReferrals_HrCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "HrCandidates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrEmployeeReferrals_HrEmployees_ReferrerEmployeeId",
                        column: x => x.ReferrerEmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrInterviewFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    InterviewId = table.Column<int>(type: "integer", nullable: false),
                    PanellistEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    PanellistName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Score = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Strengths = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Concerns = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrInterviewFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrInterviewFeedbacks_HrEmployees_PanellistEmployeeId",
                        column: x => x.PanellistEmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrInterviewFeedbacks_HrInterviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "HrInterviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrInterviewRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PassingScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrInterviewRounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrJobOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CandidateId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: true),
                    AnnualCtc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayStructureId = table.Column<int>(type: "integer", nullable: true),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedJoiningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    OutcomeReason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Terms = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrJobOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrJobOffers_HrCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "HrCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrPerformanceFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AppraisalCycleId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    GivenByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Relationship = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Rating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WhatWorksWell = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    WhatCouldImprove = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    SharedWithEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrPerformanceFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrPerformanceFeedbacks_HrAppraisalCycles_AppraisalCycleId",
                        column: x => x.AppraisalCycleId,
                        principalTable: "HrAppraisalCycles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrPerformanceFeedbacks_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrPerformanceFeedbacks_HrEmployees_GivenByEmployeeId",
                        column: x => x.GivenByEmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrStaffingPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrStaffingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrTrainingFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TrainingId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WouldRecommend = table.Column<bool>(type: "boolean", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTrainingFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTrainingFeedbacks_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrTrainingFeedbacks_HrTrainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "HrTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrAppraisalTemplateKras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AppraisalTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Weight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAppraisalTemplateKras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAppraisalTemplateKras_HrAppraisalTemplates_AppraisalTempl~",
                        column: x => x.AppraisalTemplateId,
                        principalTable: "HrAppraisalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrInterviewSkillRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    InterviewFeedbackId = table.Column<int>(type: "integer", nullable: false),
                    InterviewSkillId = table.Column<int>(type: "integer", nullable: true),
                    SkillName = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Rating = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrInterviewSkillRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrInterviewSkillRatings_HrInterviewFeedbacks_InterviewFeedb~",
                        column: x => x.InterviewFeedbackId,
                        principalTable: "HrInterviewFeedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrInterviewSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    InterviewRoundId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Weight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrInterviewSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrInterviewSkills_HrInterviewRounds_InterviewRoundId",
                        column: x => x.InterviewRoundId,
                        principalTable: "HrInterviewRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrJobRequisitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    DesignationId = table.Column<int>(type: "integer", nullable: true),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    IsReplacement = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacingEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    RequiredBy = table.Column<DateOnly>(type: "date", nullable: true),
                    Justification = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    JobDescription = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SalaryMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StaffingPlanId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DecisionNote = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VacancyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrJobRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrJobRequisitions_HrDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "HrDepartments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrJobRequisitions_HrEmployees_RequestedByEmployeeId",
                        column: x => x.RequestedByEmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrJobRequisitions_HrStaffingPlans_StaffingPlanId",
                        column: x => x.StaffingPlanId,
                        principalTable: "HrStaffingPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrJobRequisitions_HrVacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "HrVacancies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HrStaffingPlanLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    StaffingPlanId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Position = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    BudgetPerHead = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrStaffingPlanLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrStaffingPlanLines_HrDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "HrDepartments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HrStaffingPlanLines_HrStaffingPlans_StaffingPlanId",
                        column: x => x.StaffingPlanId,
                        principalTable: "HrStaffingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviews_InterviewRoundId",
                table: "HrInterviews",
                column: "InterviewRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAppraisals_AppraisalTemplateId",
                table: "HrAppraisals",
                column: "AppraisalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAppraisalKras_AppraisalId",
                table: "HrAppraisalKras",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_HrAppraisalTemplateKras_AppraisalTemplateId_Title",
                table: "HrAppraisalTemplateKras",
                columns: new[] { "AppraisalTemplateId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeReferrals_CandidateId",
                table: "HrEmployeeReferrals",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeReferrals_ReferrerEmployeeId_Status",
                table: "HrEmployeeReferrals",
                columns: new[] { "ReferrerEmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviewFeedbacks_InterviewId_PanellistName",
                table: "HrInterviewFeedbacks",
                columns: new[] { "InterviewId", "PanellistName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviewFeedbacks_PanellistEmployeeId",
                table: "HrInterviewFeedbacks",
                column: "PanellistEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviewSkillRatings_InterviewFeedbackId",
                table: "HrInterviewSkillRatings",
                column: "InterviewFeedbackId");

            migrationBuilder.CreateIndex(
                name: "IX_HrInterviewSkills_InterviewRoundId_Name",
                table: "HrInterviewSkills",
                columns: new[] { "InterviewRoundId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrJobOffers_CandidateId_Revision",
                table: "HrJobOffers",
                columns: new[] { "CandidateId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrJobRequisitions_CompanyId_Status",
                table: "HrJobRequisitions",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrJobRequisitions_DepartmentId",
                table: "HrJobRequisitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrJobRequisitions_RequestedByEmployeeId",
                table: "HrJobRequisitions",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrJobRequisitions_StaffingPlanId",
                table: "HrJobRequisitions",
                column: "StaffingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_HrJobRequisitions_VacancyId",
                table: "HrJobRequisitions",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPerformanceFeedbacks_AppraisalCycleId",
                table: "HrPerformanceFeedbacks",
                column: "AppraisalCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_HrPerformanceFeedbacks_EmployeeId_AppraisalCycleId",
                table: "HrPerformanceFeedbacks",
                columns: new[] { "EmployeeId", "AppraisalCycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrPerformanceFeedbacks_GivenByEmployeeId",
                table: "HrPerformanceFeedbacks",
                column: "GivenByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrStaffingPlanLines_DepartmentId",
                table: "HrStaffingPlanLines",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrStaffingPlanLines_StaffingPlanId_DepartmentId",
                table: "HrStaffingPlanLines",
                columns: new[] { "StaffingPlanId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrTrainingFeedbacks_EmployeeId",
                table: "HrTrainingFeedbacks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTrainingFeedbacks_TrainingId_EmployeeId",
                table: "HrTrainingFeedbacks",
                columns: new[] { "TrainingId", "EmployeeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HrAppraisals_HrAppraisalTemplates_AppraisalTemplateId",
                table: "HrAppraisals",
                column: "AppraisalTemplateId",
                principalTable: "HrAppraisalTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HrInterviews_HrInterviewRounds_InterviewRoundId",
                table: "HrInterviews",
                column: "InterviewRoundId",
                principalTable: "HrInterviewRounds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HrAppraisals_HrAppraisalTemplates_AppraisalTemplateId",
                table: "HrAppraisals");

            migrationBuilder.DropForeignKey(
                name: "FK_HrInterviews_HrInterviewRounds_InterviewRoundId",
                table: "HrInterviews");

            migrationBuilder.DropTable(
                name: "HrAppraisalKras");

            migrationBuilder.DropTable(
                name: "HrAppraisalTemplateKras");

            migrationBuilder.DropTable(
                name: "HrEmployeeReferrals");

            migrationBuilder.DropTable(
                name: "HrInterviewSkillRatings");

            migrationBuilder.DropTable(
                name: "HrInterviewSkills");

            migrationBuilder.DropTable(
                name: "HrJobOffers");

            migrationBuilder.DropTable(
                name: "HrJobRequisitions");

            migrationBuilder.DropTable(
                name: "HrPerformanceFeedbacks");

            migrationBuilder.DropTable(
                name: "HrStaffingPlanLines");

            migrationBuilder.DropTable(
                name: "HrTrainingFeedbacks");

            migrationBuilder.DropTable(
                name: "HrAppraisalTemplates");

            migrationBuilder.DropTable(
                name: "HrInterviewFeedbacks");

            migrationBuilder.DropTable(
                name: "HrInterviewRounds");

            migrationBuilder.DropTable(
                name: "HrStaffingPlans");

            migrationBuilder.DropIndex(
                name: "IX_HrInterviews_InterviewRoundId",
                table: "HrInterviews");

            migrationBuilder.DropIndex(
                name: "IX_HrAppraisals_AppraisalTemplateId",
                table: "HrAppraisals");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "HrTrainingEnrolments");

            migrationBuilder.DropColumn(
                name: "TrainerRemarks",
                table: "HrTrainingEnrolments");

            migrationBuilder.DropColumn(
                name: "InterviewRoundId",
                table: "HrInterviews");

            migrationBuilder.DropColumn(
                name: "AppraisalTemplateId",
                table: "HrAppraisals");

            migrationBuilder.DropColumn(
                name: "FinalScore",
                table: "HrAppraisals");
        }
    }
}
