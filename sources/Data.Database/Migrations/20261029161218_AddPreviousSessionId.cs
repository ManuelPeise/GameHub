using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviousSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousSessionId",
                schema: "game",
                table: "GameSession",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSession_PreviousSessionId",
                schema: "game",
                table: "GameSession",
                column: "PreviousSessionId",
                unique: true,
                filter: "\"PreviousSessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSession_PreviousSessionId",
                schema: "game",
                table: "GameSession");

            migrationBuilder.DropColumn(
                name: "PreviousSessionId",
                schema: "game",
                table: "GameSession");
        }
    }
}
