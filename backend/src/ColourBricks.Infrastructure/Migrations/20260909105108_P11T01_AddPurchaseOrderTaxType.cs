using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P11T01_AddPurchaseOrderTaxType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "PurchaseOrderLine",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            // Every pre-existing line was priced with a flat tax figure (Amount = 2) —
            // the percentage option didn't exist before this migration.
            migrationBuilder.AddColumn<sbyte>(
                name: "TaxType",
                table: "PurchaseOrderLine",
                type: "tinyint",
                nullable: false,
                defaultValue: (sbyte)2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "PurchaseOrderLine");

            migrationBuilder.DropColumn(
                name: "TaxType",
                table: "PurchaseOrderLine");
        }
    }
}
