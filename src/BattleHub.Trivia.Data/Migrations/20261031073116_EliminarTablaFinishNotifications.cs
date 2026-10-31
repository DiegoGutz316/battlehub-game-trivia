using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleHub.Trivia.Data.Migrations
{
    /// <inheritdoc />
    public partial class EliminarTablaFinishNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinishNotifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinishNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Blocked = table.Column<bool>(type: "bit", nullable: false),
                    Delivered = table.Column<bool>(type: "bit", nullable: false),
                    MatchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchRefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinishNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinishNotifications_MatchResults_MatchRefId",
                        column: x => x.MatchRefId,
                        principalTable: "MatchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinishNotifications_Delivered_NextAttemptAt",
                table: "FinishNotifications",
                columns: new[] { "Delivered", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinishNotifications_MatchId",
                table: "FinishNotifications",
                column: "MatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinishNotifications_MatchRefId",
                table: "FinishNotifications",
                column: "MatchRefId",
                unique: true);
        }
    }
}
