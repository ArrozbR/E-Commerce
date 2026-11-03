using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HeightCm",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "LengthCm",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "products",
                type: "numeric(6,3)",
                precision: 6,
                scale: 3,
                nullable: false,
                defaultValue: 0.8m);

            migrationBuilder.AddColumn<int>(
                name: "WidthCm",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 6, 20, 0.8m, 30 });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 4, 12, 0.3m, 20 });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 6, 20, 0.8m, 30 });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 4, 12, 0.3m, 20 });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 6, 20, 0.8m, 30 });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"),
                columns: new[] { "HeightCm", "LengthCm", "WeightKg", "WidthCm" },
                values: new object[] { 4, 12, 0.3m, 20 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "products");

            migrationBuilder.DropColumn(
                name: "LengthCm",
                table: "products");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "products");

            migrationBuilder.DropColumn(
                name: "WidthCm",
                table: "products");
        }
    }
}
