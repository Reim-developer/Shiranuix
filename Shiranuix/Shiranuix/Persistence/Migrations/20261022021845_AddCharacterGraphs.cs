using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddCharacterGraphs : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "Graphs",
                columns: table => new {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Graphs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GraphCharacters",
                columns: table => new {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GraphId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_GraphCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GraphCharacters_Graphs_GraphId",
                        column: x => x.GraphId,
                        principalTable: "Graphs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GraphRelationships",
                columns: table => new {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GraphId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_GraphRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GraphRelationships_GraphCharacters_FromCharacterId",
                        column: x => x.FromCharacterId,
                        principalTable: "GraphCharacters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GraphRelationships_GraphCharacters_ToCharacterId",
                        column: x => x.ToCharacterId,
                        principalTable: "GraphCharacters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GraphRelationships_Graphs_GraphId",
                        column: x => x.GraphId,
                        principalTable: "Graphs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GraphCharacters_GraphId",
                table: "GraphCharacters",
                column: "GraphId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphRelationships_FromCharacterId",
                table: "GraphRelationships",
                column: "FromCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphRelationships_GraphId",
                table: "GraphRelationships",
                column: "GraphId");

            migrationBuilder.CreateIndex(
                name: "IX_GraphRelationships_ToCharacterId",
                table: "GraphRelationships",
                column: "ToCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Graphs_Name",
                table: "Graphs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "GraphRelationships");

            migrationBuilder.DropTable(
                name: "GraphCharacters");

            migrationBuilder.DropTable(
                name: "Graphs");
        }
    }
}
