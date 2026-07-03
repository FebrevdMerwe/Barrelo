using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Darts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThrowRecordPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PositionX",
                table: "ThrowRecords",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PositionY",
                table: "ThrowRecords",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "ThrowRecords");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "ThrowRecords");
        }
    }
}
