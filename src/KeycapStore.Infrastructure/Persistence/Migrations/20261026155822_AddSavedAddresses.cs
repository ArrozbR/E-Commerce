using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeycapStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_addresses",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_addresses", x => x.CustomerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_addresses");
        }
    }
}
