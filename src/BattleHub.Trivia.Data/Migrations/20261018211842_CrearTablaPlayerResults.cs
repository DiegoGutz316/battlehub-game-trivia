using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Trivia.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaPlayerResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchRefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswersTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerResults_MatchResults_MatchRefId",
                        column: x => x.MatchRefId,
                        principalTable: "MatchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerResults_MatchId_UserId",
                table: "PlayerResults",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerResults_MatchRefId",
                table: "PlayerResults",
                column: "MatchRefId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerResults_UserId",
                table: "PlayerResults",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerResults");
        }
    }
}
