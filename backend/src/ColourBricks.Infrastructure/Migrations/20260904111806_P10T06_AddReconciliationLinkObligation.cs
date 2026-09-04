using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P10T06_AddReconciliationLinkObligation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ObligationId",
                table: "ReconciliationLink",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationLink_ObligationId",
                table: "ReconciliationLink",
                column: "ObligationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReconciliationLink_Obligation_ObligationId",
                table: "ReconciliationLink",
                column: "ObligationId",
                principalTable: "Obligation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReconciliationLink_Obligation_ObligationId",
                table: "ReconciliationLink");

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationLink_ObligationId",
                table: "ReconciliationLink");

            migrationBuilder.DropColumn(
                name: "ObligationId",
                table: "ReconciliationLink");
        }
    }
}
