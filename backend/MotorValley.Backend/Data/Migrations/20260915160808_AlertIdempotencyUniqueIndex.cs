using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotorValley.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlertIdempotencyUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CriticalAlerts_MachineId",
                table: "CriticalAlerts");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlerts_MachineId_Timestamp",
                table: "CriticalAlerts",
                columns: new[] { "MachineId", "Timestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CriticalAlerts_MachineId_Timestamp",
                table: "CriticalAlerts");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlerts_MachineId",
                table: "CriticalAlerts",
                column: "MachineId");
        }
    }
}
