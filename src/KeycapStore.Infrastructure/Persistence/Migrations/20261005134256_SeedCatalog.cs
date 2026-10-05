using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "products",
                columns: new[] { "Id", "Name", "Price", "Stock" },
                values: new object[,]
                {
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), "Kit Aurora (base)", 349.90m, 40 },
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"), "Kit Aurora (novelties)", 129.90m, 3 },
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"), "Kit Maré (base)", 319.90m, 25 },
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"), "Kit Maré (modificadores)", 149.90m, 0 },
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"), "Kit Zéfiro (base)", 299.90m, 12 },
                    { new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"), "Kit Zéfiro (teclas de função)", 89.90m, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e03"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e04"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "Id",
                keyValue: new Guid("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06"));
        }
    }
}
