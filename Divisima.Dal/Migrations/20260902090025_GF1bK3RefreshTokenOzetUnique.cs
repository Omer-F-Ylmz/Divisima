using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class GF1bK3RefreshTokenOzetUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_refresh_token",
                table: "user_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_refresh_token",
                table: "user_sessions",
                column: "refresh_token",
                unique: true,
                filter: "[refresh_token] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_refresh_token",
                table: "user_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_refresh_token",
                table: "user_sessions",
                column: "refresh_token");
        }
    }
}
