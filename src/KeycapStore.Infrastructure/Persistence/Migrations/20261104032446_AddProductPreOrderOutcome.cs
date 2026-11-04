using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPreOrderOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreOrderOutcome",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"),
                column: "PreOrderOutcome",
                value: null);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"),
                column: "PreOrderOutcome",
                value: null);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"),
                column: "PreOrderOutcome",
                value: null);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"),
                column: "PreOrderOutcome",
                value: null);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"),
                column: "PreOrderOutcome",
                value: null);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"),
                column: "PreOrderOutcome",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreOrderOutcome",
                table: "products");
        }
    }
}
