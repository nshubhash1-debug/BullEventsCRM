using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Connectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    InboundToken = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultBranchId = table.Column<int>(type: "integer", nullable: true),
                    DefaultOwnerId = table.Column<int>(type: "integer", nullable: true),
                    SourceLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "crm_ci"),
                    LastEventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Connectors_Branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Connectors_Users_DefaultOwnerId",
                        column: x => x.DefaultOwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    Outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, collation: "crm_ci"),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "crm_ci"),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectorEvents_Connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalTable: "Connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorEvents_ConnectorId_At",
                table: "ConnectorEvents",
                columns: new[] { "ConnectorId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_CompanyId_Provider",
                table: "Connectors",
                columns: new[] { "CompanyId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_DefaultBranchId",
                table: "Connectors",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_DefaultOwnerId",
                table: "Connectors",
                column: "DefaultOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Connectors_InboundToken",
                table: "Connectors",
                column: "InboundToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorEvents");

            migrationBuilder.DropTable(
                name: "Connectors");
        }
    }
}
