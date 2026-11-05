using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPalette : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaletteAccent",
                table: "products",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#a1a1aa");

            migrationBuilder.AddColumn<string>(
                name: "PaletteKeys",
                table: "products",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#e4e4e7");

            migrationBuilder.AddColumn<string>(
                name: "PaletteLegends",
                table: "products",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#3f3f46");

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#8b5cf6", "#1e293b", "#a7f3d0" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#22d3ee", "#1e293b", "#a7f3d0" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#0ea5e9", "#e0f2fe", "#0c4a6e" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#0ea5e9", "#0c4a6e", "#e0f2fe" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#f97316", "#f5f0e6", "#3f3f46" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"),
                columns: new[] { "PaletteAccent", "PaletteKeys", "PaletteLegends" },
                values: new object[] { "#f97316", "#3f3f46", "#f5f0e6" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaletteAccent",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PaletteKeys",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PaletteLegends",
                table: "products");
        }
    }
}
