using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSForge.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadAlertsAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeadAlerts_LeadId",
                table: "LeadAlerts");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "LeadAlerts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "LeadAlerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_LeadAlerts_BusinessId_IsResolved_CreatedAtUtc",
                table: "LeadAlerts",
                columns: new[] { "BusinessId", "IsResolved", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadAlerts_LeadId_Type_IsResolved",
                table: "LeadAlerts",
                columns: new[] { "LeadId", "Type", "IsResolved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeadAlerts_BusinessId_IsResolved_CreatedAtUtc",
                table: "LeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_LeadAlerts_LeadId_Type_IsResolved",
                table: "LeadAlerts");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "LeadAlerts");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "LeadAlerts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_LeadAlerts_LeadId",
                table: "LeadAlerts",
                column: "LeadId");
        }
    }
}
