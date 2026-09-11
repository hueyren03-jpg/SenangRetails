using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SenangRetails.Data.Sqlite.Database.Migrations;

[DbContext(typeof(LocalAppDbContext))]
[Migration("202608210002_DurableCashSaleSync")]
public sealed class DurableCashSaleSync : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastAttemptAtUtc",
            table: "OfflineCashSales",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextAttemptAtUtc",
            table: "OfflineCashSales",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OfflineCashSales_NextAttemptAtUtc",
            table: "OfflineCashSales",
            column: "NextAttemptAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OfflineCashSales_NextAttemptAtUtc",
            table: "OfflineCashSales");
        migrationBuilder.DropColumn(name: "LastAttemptAtUtc", table: "OfflineCashSales");
        migrationBuilder.DropColumn(name: "NextAttemptAtUtc", table: "OfflineCashSales");
    }
}
