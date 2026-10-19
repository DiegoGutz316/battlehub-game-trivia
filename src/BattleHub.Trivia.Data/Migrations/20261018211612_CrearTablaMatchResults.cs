using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Trivia.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaMatchResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GameType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WinnerUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsTie = table.Column<bool>(type: "bit", nullable: false),
                    DecidedByTiebreak = table.Column<bool>(type: "bit", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.CheckConstraint("CK_MatchResults_Metadata_IsJson", "ISJSON([Metadata]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_MatchId",
                table: "MatchResults",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResults");
        }
    }
}
