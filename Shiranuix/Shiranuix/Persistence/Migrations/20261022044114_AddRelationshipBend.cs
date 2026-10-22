using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddRelationshipBend : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<double>(
                name: "Bend",
                table: "GraphRelationships",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Bend",
                table: "GraphRelationships");
        }
    }
}
