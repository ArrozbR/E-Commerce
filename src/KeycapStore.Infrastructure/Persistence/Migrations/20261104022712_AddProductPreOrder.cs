using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPreOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreOrderEndsAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreOrderMinimumUnits",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreOrderShipsAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"),
                columns: new[] { "PreOrderEndsAt", "PreOrderMinimumUnits", "PreOrderShipsAt" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreOrderEndsAt",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PreOrderMinimumUnits",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PreOrderShipsAt",
                table: "products");
        }
    }
}
