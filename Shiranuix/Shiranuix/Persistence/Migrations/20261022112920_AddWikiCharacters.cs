using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddWikiCharacters : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            // The wiki comes first, because the characters already in the
            // store are about to be asked to stand in it: a character
            // written down before the wiki existed must not be lost with
            // the columns that held them.
            migrationBuilder.CreateTable(
                name: "WikiCharacters",
                columns: table => new {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BirthDate = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_WikiCharacters", x => x.Id);
                });

            // The link is made nullable first, so that the characters
            // already in the store can be pointed at their wiki entries
            // before the column is made to insist on it.
            migrationBuilder.AddColumn<int>(
                name: "WikiCharacterId",
                table: "GraphCharacters",
                type: "INTEGER",
                nullable: true);

            // Every character written down before the wiki existed gets an
            // entry of their own — the name they had and the note the
            // writer kept of them — and stands for it. The entry takes
            // the character's own row id, so that the two can never be
            // matched up wrongly: a character with the same name as
            // another is still two characters, and each keeps its own.
            migrationBuilder.Sql(
                """
                INSERT INTO "WikiCharacters" ("Id", "Name", "BirthDate", "Description", "Note", "CreatedUtc", "UpdatedUtc")
                SELECT "Id", "Name", '', '', "Note", strftime('%Y-%m-%d %H:%M:%f', 'now'), strftime('%Y-%m-%d %H:%M:%f', 'now')
                FROM "GraphCharacters";

                UPDATE "GraphCharacters" SET "WikiCharacterId" = "Id";
                """);

            // And now the column may insist: every node stands for a
            // character the wiki keeps.
            migrationBuilder.AlterColumn<int>(
                name: "WikiCharacterId",
                table: "GraphCharacters",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GraphCharacters_WikiCharacterId",
                table: "GraphCharacters",
                column: "WikiCharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_GraphCharacters_WikiCharacters_WikiCharacterId",
                table: "GraphCharacters",
                column: "WikiCharacterId",
                principalTable: "WikiCharacters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // The name and the note now live in the wiki, where the
            // character is kept — so the columns that held them here are
            // taken away, and nothing is lost that the wiki has not taken
            // in first.
            migrationBuilder.DropColumn(
                name: "Name",
                table: "GraphCharacters");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "GraphCharacters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_GraphCharacters_WikiCharacters_WikiCharacterId",
                table: "GraphCharacters");

            migrationBuilder.DropTable(
                name: "WikiCharacters");

            migrationBuilder.DropIndex(
                name: "IX_GraphCharacters_WikiCharacterId",
                table: "GraphCharacters");

            migrationBuilder.DropColumn(
                name: "WikiCharacterId",
                table: "GraphCharacters");

            // The name and the note come back as empty columns: what they
            // held now lives in the wiki, and the wiki is dropped here.
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "GraphCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "GraphCharacters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
