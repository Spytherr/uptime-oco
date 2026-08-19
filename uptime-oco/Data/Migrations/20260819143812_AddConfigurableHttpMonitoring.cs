using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uptime_oco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableHttpMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "PingResults",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedStatusCode",
                table: "Monitors",
                type: "INTEGER",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "Monitors",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "PingResults");

            migrationBuilder.DropColumn(
                name: "ExpectedStatusCode",
                table: "Monitors");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "Monitors");
        }
    }
}
