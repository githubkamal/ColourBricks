using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P9T02_PerfIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_AccountId_EntryDate",
                table: "LedgerEntry",
                columns: new[] { "AccountId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntry_CategoryId",
                table: "LedgerEntry",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransaction_Status_ValueDate",
                table: "BankTransaction",
                columns: new[] { "Status", "ValueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntry_AccountId_EntryDate",
                table: "LedgerEntry");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntry_CategoryId",
                table: "LedgerEntry");

            migrationBuilder.DropIndex(
                name: "IX_BankTransaction_Status_ValueDate",
                table: "BankTransaction");
        }
    }
}
