using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Web.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class DurableMaintenanceQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "BackgroundJobRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                table: "BackgroundJobRuns",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                table: "BackgroundJobRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRuns_Status_AvailableAt",
                table: "BackgroundJobRuns",
                columns: new[] { "Status", "AvailableAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobRuns_Status_AvailableAt",
                table: "BackgroundJobRuns");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "BackgroundJobRuns");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "BackgroundJobRuns");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "BackgroundJobRuns");
        }
    }
}
