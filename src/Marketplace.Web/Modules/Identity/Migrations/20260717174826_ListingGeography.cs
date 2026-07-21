using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Web.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class ListingGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ExactAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExactLatitude = table.Column<decimal>(type: "numeric", nullable: true),
                    ExactLongitude = table.Column<decimal>(type: "numeric", nullable: true),
                    PublicLatitude = table.Column<decimal>(type: "numeric", nullable: false),
                    PublicLongitude = table.Column<decimal>(type: "numeric", nullable: false),
                    IsExactPointPublic = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingLocations_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingLocations_ListingId",
                table: "ListingLocations",
                column: "ListingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingLocations");
        }
    }
}
