using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calamus.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChosenProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChosenProvider",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChosenProvider",
                table: "Users");
        }
    }
}
