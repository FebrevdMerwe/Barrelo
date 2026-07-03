using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Darts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerGroupsAndMatchWinners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WinnerPlayerId",
                table: "Matches");

            migrationBuilder.AddColumn<int>(
                name: "GroupIndex",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WinnerPlayerIds",
                table: "Matches",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupIndex",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "WinnerPlayerIds",
                table: "Matches");

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerPlayerId",
                table: "Matches",
                type: "TEXT",
                nullable: true);
        }
    }
}
