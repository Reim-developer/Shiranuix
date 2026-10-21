using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddWelcomeMessage : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            // The invitation arrives on, for the rows that saw it not: the
            // writer's sheet still greets exactly as it did before this
            // column stood, and the choice to send the greeting away is
            // theirs to make now - what the update is about, kept by the
            // rows already there.
            migrationBuilder.AddColumn<bool>(
                name: "ShowWelcomeMessage",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "ShowWelcomeMessage",
                table: "UserSettings");
        }
    }
}
