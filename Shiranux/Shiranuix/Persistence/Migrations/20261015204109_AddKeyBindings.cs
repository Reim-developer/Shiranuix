using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddKeyBindings : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "KeyBindings",
                columns: table => new {
                    Command = table.Column<string>(type: "TEXT", nullable: false),
                    Gesture = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_KeyBindings", x => x.Command);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "KeyBindings");
        }
    }
}
