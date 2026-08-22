using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uptime_oco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPingHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PingResults_MonitorId",
                table: "PingResults");

            migrationBuilder.CreateIndex(
                name: "IX_PingResults_MonitorId_CheckedAt",
                table: "PingResults",
                columns: new[] { "MonitorId", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PingResults_MonitorId_CheckedAt",
                table: "PingResults");

            migrationBuilder.CreateIndex(
                name: "IX_PingResults_MonitorId",
                table: "PingResults",
                column: "MonitorId");
        }
    }
}
