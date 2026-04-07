using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSForge.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadFlowPhase1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InquirySummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastIncomingMessagePreview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastContactAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReplyAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastIncomingAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowUpAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivities_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadAiSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputContext = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    OutputText = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    Tone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Goal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadAiSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadAiSuggestions_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    AiTone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AiGoal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AiModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadMessages_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadNotes_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadTagMaps",
                columns: table => new
                {
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTagMaps", x => new { x.LeadId, x.TagId });
                    table.ForeignKey(
                        name: "FK_LeadTagMaps_LeadTags_TagId",
                        column: x => x.TagId,
                        principalTable: "LeadTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTagMaps_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_BusinessId_LeadId_CreatedAtUtc",
                table: "LeadActivities",
                columns: new[] { "BusinessId", "LeadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_LeadId",
                table: "LeadActivities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadAiSuggestions_BusinessId_LeadId_CreatedAtUtc",
                table: "LeadAiSuggestions",
                columns: new[] { "BusinessId", "LeadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadAiSuggestions_LeadId",
                table: "LeadAiSuggestions",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadMessages_BusinessId_CreatedAtUtc",
                table: "LeadMessages",
                columns: new[] { "BusinessId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadMessages_BusinessId_LeadId_CreatedAtUtc",
                table: "LeadMessages",
                columns: new[] { "BusinessId", "LeadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadMessages_LeadId",
                table: "LeadMessages",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadNotes_BusinessId_LeadId_CreatedAtUtc",
                table: "LeadNotes",
                columns: new[] { "BusinessId", "LeadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadNotes_LeadId",
                table: "LeadNotes",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_CreatedAtUtc",
                table: "Leads",
                columns: new[] { "BusinessId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_Email",
                table: "Leads",
                columns: new[] { "BusinessId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_IsArchived",
                table: "Leads",
                columns: new[] { "BusinessId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_NextFollowUpAtUtc",
                table: "Leads",
                columns: new[] { "BusinessId", "NextFollowUpAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_Phone",
                table: "Leads",
                columns: new[] { "BusinessId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_Source",
                table: "Leads",
                columns: new[] { "BusinessId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_BusinessId_Status",
                table: "Leads",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadTagMaps_TagId",
                table: "LeadTagMaps",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTags_BusinessId_Name",
                table: "LeadTags",
                columns: new[] { "BusinessId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadActivities");

            migrationBuilder.DropTable(
                name: "LeadAiSuggestions");

            migrationBuilder.DropTable(
                name: "LeadMessages");

            migrationBuilder.DropTable(
                name: "LeadNotes");

            migrationBuilder.DropTable(
                name: "LeadTagMaps");

            migrationBuilder.DropTable(
                name: "LeadTags");

            migrationBuilder.DropTable(
                name: "Leads");
        }
    }
}
