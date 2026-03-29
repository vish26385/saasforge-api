using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSForge.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFieldsToBusinessSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "BusinessSubscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "BusinessSubscriptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "BusinessSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderOrderId",
                table: "BusinessSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                table: "BusinessSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSubscriptions_ProviderPaymentId",
                table: "BusinessSubscriptions",
                column: "ProviderPaymentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessSubscriptions_ProviderPaymentId",
                table: "BusinessSubscriptions");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "BusinessSubscriptions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BusinessSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "BusinessSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderOrderId",
                table: "BusinessSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                table: "BusinessSubscriptions");
        }
    }
}