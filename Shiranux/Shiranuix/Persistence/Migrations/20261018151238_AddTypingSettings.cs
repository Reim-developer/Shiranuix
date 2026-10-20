using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddTypingSettings : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            // The typing turns arrive on, for the rows that saw them not:
            // the quotes take their curly selves at once, and the pair of
            // hyphens becomes the long dash - the features the update is
            // about, kept by the pages that already stand.
            migrationBuilder.AddColumn<bool>(
                name: "SmartQuotes",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DashMode",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "DashMode",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "SmartQuotes",
                table: "UserSettings");
        }
    }
}
