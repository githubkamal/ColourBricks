using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P14T01_FieldOfficerProjectExpense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "FieldOfficerExpense",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                table: "FieldOfficerExpense",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldOfficerExpense_ProjectId",
                table: "FieldOfficerExpense",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldOfficerExpense_Project_ProjectId",
                table: "FieldOfficerExpense",
                column: "ProjectId",
                principalTable: "Project",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldOfficerExpense_Project_ProjectId",
                table: "FieldOfficerExpense");

            migrationBuilder.DropIndex(
                name: "IX_FieldOfficerExpense_ProjectId",
                table: "FieldOfficerExpense");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "FieldOfficerExpense");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "FieldOfficerExpense");
        }
    }
}
