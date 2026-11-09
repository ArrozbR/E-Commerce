using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxEmailCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "outbox_emails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "outbox_emails",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "outbox_emails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_emails_CustomerId_Kind_CreatedAt",
                table: "outbox_emails",
                columns: new[] { "CustomerId", "Kind", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_outbox_emails_order_or_customer",
                table: "outbox_emails",
                sql: "\"OrderId\" IS NOT NULL OR \"CustomerId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_emails_CustomerId_Kind_CreatedAt",
                table: "outbox_emails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_outbox_emails_order_or_customer",
                table: "outbox_emails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "outbox_emails");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "outbox_emails");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "outbox_emails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
