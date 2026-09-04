using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P10T02_ReconciliationLinkKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "SettlementId",
                table: "ReconciliationLink",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "CommonExpenseId",
                table: "ReconciliationLink",
                type: "bigint",
                nullable: true);

            // Every existing row is a Settlement link (CommonExpense-kind links are new in
            // this migration) — default to 1 (ReconciliationLinkKind.Settlement), not the
            // CLR-default 0, which is not a valid enum member.
            migrationBuilder.AddColumn<sbyte>(
                name: "Kind",
                table: "ReconciliationLink",
                type: "tinyint",
                nullable: false,
                defaultValue: (sbyte)1);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationLink_CommonExpenseId",
                table: "ReconciliationLink",
                column: "CommonExpenseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReconciliationLink_CommonExpense_CommonExpenseId",
                table: "ReconciliationLink",
                column: "CommonExpenseId",
                principalTable: "CommonExpense",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReconciliationLink_CommonExpense_CommonExpenseId",
                table: "ReconciliationLink");

            migrationBuilder.DropIndex(
                name: "IX_ReconciliationLink_CommonExpenseId",
                table: "ReconciliationLink");

            migrationBuilder.DropColumn(
                name: "CommonExpenseId",
                table: "ReconciliationLink");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ReconciliationLink");

            migrationBuilder.AlterColumn<long>(
                name: "SettlementId",
                table: "ReconciliationLink",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
