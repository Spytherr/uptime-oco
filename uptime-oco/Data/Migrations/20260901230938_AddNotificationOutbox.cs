using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uptime_oco.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationOutboxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncidentId = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationOutboxes_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationOutboxes_NotificationChannels_NotificationChannelId",
                        column: x => x.NotificationChannelId,
                        principalTable: "NotificationChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_IncidentId",
                table: "NotificationOutboxes",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_NotificationChannelId",
                table: "NotificationOutboxes",
                column: "NotificationChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_ProcessedAt_FailedAt_NextAttemptAt",
                table: "NotificationOutboxes",
                columns: new[] { "ProcessedAt", "FailedAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationOutboxes");
        }
    }
}
