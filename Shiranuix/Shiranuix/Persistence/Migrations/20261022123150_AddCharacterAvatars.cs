using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddCharacterAvatars : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            // A character's face, kept as the picture itself: the bytes of
            // the image the writer chose, in the row of the character they
            // belong to — so the face travels with the character and
            // nothing has to be pointed at.
            migrationBuilder.AddColumn<byte[]>(
                name: "Avatar",
                table: "WikiCharacters",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "WikiCharacters");
        }
    }
}
