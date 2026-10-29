using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Data.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.CreateTable(
                name: "GameSession",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentSeatId = table.Column<Guid>(type: "uuid", nullable: true),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    StateVersion = table.Column<int>(type: "integer", nullable: false),
                    RngSeed = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSession_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seat",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiDifficulty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AiStrategyVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ConnectionStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seat", x => x.Id);
                    table.CheckConstraint("CK_Seat_HumanHasUser_AiHasDifficulty", "(\"Kind\" = 'Human' AND \"UserId\" IS NOT NULL AND \"AiDifficulty\" IS NULL AND \"AiStrategyVersion\" IS NULL) OR (\"Kind\" = 'Ai' AND \"UserId\" IS NULL AND \"AiDifficulty\" IS NOT NULL AND \"AiStrategyVersion\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Seat_GameSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "game",
                        principalTable: "GameSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seat_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameAction",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    RandomResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    AiStrategyVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameAction_GameSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "game",
                        principalTable: "GameSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameAction_Seat_SeatId",
                        column: x => x.SeatId,
                        principalSchema: "game",
                        principalTable: "Seat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameResult",
                schema: "game",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placement = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameResult", x => new { x.SessionId, x.SeatId });
                    table.ForeignKey(
                        name: "FK_GameResult_GameSession_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "game",
                        principalTable: "GameSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameResult_Seat_SeatId",
                        column: x => x.SeatId,
                        principalSchema: "game",
                        principalTable: "Seat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameAction_SeatId",
                schema: "game",
                table: "GameAction",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAction_SessionId_Sequence",
                schema: "game",
                table: "GameAction",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameResult_SeatId",
                schema: "game",
                table: "GameResult",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSession_OneOpenSingleplayerPerOwnerAndGame",
                schema: "game",
                table: "GameSession",
                columns: new[] { "OwnerUserId", "GameType" },
                unique: true,
                filter: "\"Mode\" = 'Singleplayer' AND \"Status\" IN ('Running', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_GameSession_Status_LastActivityAt",
                schema: "game",
                table: "GameSession",
                columns: new[] { "Status", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Seat_SessionId_Order",
                schema: "game",
                table: "Seat",
                columns: new[] { "SessionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seat_UserId",
                schema: "game",
                table: "Seat",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameAction",
                schema: "game");

            migrationBuilder.DropTable(
                name: "GameResult",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Seat",
                schema: "game");

            migrationBuilder.DropTable(
                name: "GameSession",
                schema: "game");
        }
    }
}
