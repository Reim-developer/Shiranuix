using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiranuix.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeTextAlignSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The fourth way of setting a line is gone — the format has
            // never been able to carry one, and the bar no longer offers
            // it — so a page saved on it would be read back by a reader
            // that has no name for it. Setting such a row back to where a
            // page starts is the only answer left that means anything: the
            // letters it drew are already flush left's to draw, since the
            // spread never reached the file.
            migrationBuilder.Sql(
                "UPDATE \"UserSettings\" SET \"TextAlign\" = 'Left' WHERE \"TextAlign\" = 'Justify'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
