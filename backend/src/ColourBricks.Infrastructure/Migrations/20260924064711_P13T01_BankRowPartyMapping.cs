using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P13T01_BankRowPartyMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ProjectId",
                table: "StagedBankRowAllocation",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "PartyId",
                table: "StagedBankRowAllocation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<sbyte>(
                name: "Target",
                table: "StagedBankRowAllocation",
                type: "tinyint",
                nullable: false,
                defaultValue: (sbyte)1); // existing rows are all project mappings (BankRowMappingTarget.Project)

            migrationBuilder.AlterColumn<long>(
                name: "ProjectId",
                table: "BankTransactionProjectHint",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "PartyId",
                table: "BankTransactionProjectHint",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<sbyte>(
                name: "Target",
                table: "BankTransactionProjectHint",
                type: "tinyint",
                nullable: false,
                defaultValue: (sbyte)1); // existing rows are all project mappings (BankRowMappingTarget.Project)

            migrationBuilder.CreateIndex(
                name: "IX_StagedBankRowAllocation_PartyId",
                table: "StagedBankRowAllocation",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionProjectHint_PartyId",
                table: "BankTransactionProjectHint",
                column: "PartyId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactionProjectHint_Party_PartyId",
                table: "BankTransactionProjectHint",
                column: "PartyId",
                principalTable: "Party",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StagedBankRowAllocation_Party_PartyId",
                table: "StagedBankRowAllocation",
                column: "PartyId",
                principalTable: "Party",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactionProjectHint_Party_PartyId",
                table: "BankTransactionProjectHint");

            migrationBuilder.DropForeignKey(
                name: "FK_StagedBankRowAllocation_Party_PartyId",
                table: "StagedBankRowAllocation");

            migrationBuilder.DropIndex(
                name: "IX_StagedBankRowAllocation_PartyId",
                table: "StagedBankRowAllocation");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactionProjectHint_PartyId",
                table: "BankTransactionProjectHint");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "StagedBankRowAllocation");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "StagedBankRowAllocation");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "BankTransactionProjectHint");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "BankTransactionProjectHint");

            migrationBuilder.AlterColumn<long>(
                name: "ProjectId",
                table: "StagedBankRowAllocation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ProjectId",
                table: "BankTransactionProjectHint",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
