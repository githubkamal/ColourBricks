using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P10T20_AddSystemSettingsLogoAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompanyLogoAttachmentId",
                table: "SystemSettings",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyLogoAttachmentId",
                table: "SystemSettings");
        }
    }
}
