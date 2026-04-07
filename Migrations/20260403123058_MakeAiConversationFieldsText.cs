using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSForge.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeAiConversationFieldsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prompt",
                table: "AiConversations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_Status_NextFollowUpAtUtc",
                table: "Leads",
                columns: new[] { "BusinessId", "Status", "NextFollowUpAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leads_BusinessId_Status_NextFollowUpAtUtc",
                table: "Leads");

            migrationBuilder.AlterColumn<string>(
                name: "Prompt",
                table: "AiConversations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
