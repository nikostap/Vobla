using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Web.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class DurableOtpChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OneTimeCodeChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Salt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: false),
                    DebugCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResendAvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneTimeCodeChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OneTimeCodeChallenges_ExpiresAt",
                table: "OneTimeCodeChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OneTimeCodeChallenges_NormalizedEmail",
                table: "OneTimeCodeChallenges",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OneTimeCodeChallenges");
        }
    }
}
