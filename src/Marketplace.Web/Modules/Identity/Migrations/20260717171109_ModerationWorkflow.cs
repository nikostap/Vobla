using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Web.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class ModerationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModerationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RuleSetVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationAppeals_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProblemField = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RuleReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CorrectionInstruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationDecisions_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RiskCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    Fragment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationFindings_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAppeals_ModerationCaseId",
                table: "ModerationAppeals",
                column: "ModerationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_ListingId",
                table: "ModerationCases",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_Status_RiskLevel_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "Status", "RiskLevel", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationDecisions_ModerationCaseId",
                table: "ModerationDecisions",
                column: "ModerationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationFindings_ModerationCaseId",
                table: "ModerationFindings",
                column: "ModerationCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationAppeals");

            migrationBuilder.DropTable(
                name: "ModerationDecisions");

            migrationBuilder.DropTable(
                name: "ModerationFindings");

            migrationBuilder.DropTable(
                name: "ModerationCases");
        }
    }
}
