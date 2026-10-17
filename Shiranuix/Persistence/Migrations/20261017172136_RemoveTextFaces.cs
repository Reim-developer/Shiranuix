using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class RemoveTextFaces : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Bold",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "Italic",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "Underline",
                table: "UserSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<bool>(
                name: "Bold",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Italic",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Underline",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
