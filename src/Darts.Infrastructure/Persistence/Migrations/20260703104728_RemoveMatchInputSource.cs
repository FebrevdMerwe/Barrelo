using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Darts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMatchInputSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputSource",
                table: "Matches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputSource",
                table: "Matches",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
