using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColourBricks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P15T01_TeamMasterAndWorkEntryNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlternatePhone",
                table: "Party",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountHolderName",
                table: "Party",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Party",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BankIfsc",
                table: "Party",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Party",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "WorkingArea",
                table: "Party",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EntryNumber",
                table: "Obligation",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TeamSite",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PartyId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    SiteLocation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContractValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSite", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamSite_Party_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Party",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamSite_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Obligation_EntryNumber",
                table: "Obligation",
                column: "EntryNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSite_PartyId",
                table: "TeamSite",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSite_ProjectId",
                table: "TeamSite",
                column: "ProjectId");

            // Give existing (active) labour work entries a reference number,
            // LW-{year}-{nnnnn}, numbered by date within each year. Type 2 =
            // ObligationType.SubcontractorWork, Status 1 = ObligationStatus.Active.
            migrationBuilder.Sql(
                """
                UPDATE `Obligation` o
                JOIN (
                    SELECT `Id`, ROW_NUMBER() OVER (PARTITION BY YEAR(`Date`) ORDER BY `Date`, `Id`) AS rn
                    FROM `Obligation`
                    WHERE `Type` = 2 AND `Status` = 1
                ) n ON n.`Id` = o.`Id`
                SET o.`EntryNumber` = CONCAT('LW-', YEAR(o.`Date`), '-', LPAD(n.rn, 5, '0'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamSite");

            migrationBuilder.DropIndex(
                name: "IX_Obligation_EntryNumber",
                table: "Obligation");

            migrationBuilder.DropColumn(
                name: "AlternatePhone",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "BankAccountHolderName",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "BankIfsc",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "WorkingArea",
                table: "Party");

            migrationBuilder.DropColumn(
                name: "EntryNumber",
                table: "Obligation");
        }
    }
}
