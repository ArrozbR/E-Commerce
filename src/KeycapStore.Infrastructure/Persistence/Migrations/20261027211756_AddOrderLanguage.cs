using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "pt-BR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "orders");
        }
    }
}
