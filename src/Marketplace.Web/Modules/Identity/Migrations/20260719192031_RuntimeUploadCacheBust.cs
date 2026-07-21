using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Web.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RuntimeUploadCacheBust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ListingMedia"
                SET "Url" = "Url" || '?v=20260719-runtime-upload-fix'
                WHERE "Url" LIKE '/uploads/%' AND POSITION('?' IN "Url") = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ListingMedia"
                SET "Url" = LEFT("Url", LENGTH("Url") - LENGTH('?v=20260719-runtime-upload-fix'))
                WHERE "Url" LIKE '%?v=20260719-runtime-upload-fix';
                """);
        }
    }
}
