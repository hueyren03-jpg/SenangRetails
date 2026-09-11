using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SenangRetails.Data.Sqlite.Database.Migrations;

[DbContext(typeof(LocalAppDbContext))]
[Migration("202608240001_ConfirmOnlineSaleVisibility")]
public sealed class ConfirmOnlineSaleVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsOnlineVisibilityConfirmed",
            table: "OfflineCashSales",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsOnlineVisibilityConfirmed",
            table: "OfflineCashSales");
    }
}
