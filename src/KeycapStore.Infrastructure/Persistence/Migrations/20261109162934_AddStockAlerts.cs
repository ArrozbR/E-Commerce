using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "outbox_emails",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_alerts",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_alerts", x => new { x.CustomerId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_stock_alerts_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_emails_ProductId",
                table: "outbox_emails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_alerts_ProductId",
                table: "stock_alerts",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_outbox_emails_products_ProductId",
                table: "outbox_emails",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outbox_emails_products_ProductId",
                table: "outbox_emails");

            migrationBuilder.DropTable(
                name: "stock_alerts");

            migrationBuilder.DropIndex(
                name: "IX_outbox_emails_ProductId",
                table: "outbox_emails");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "outbox_emails");
        }
    }
}
