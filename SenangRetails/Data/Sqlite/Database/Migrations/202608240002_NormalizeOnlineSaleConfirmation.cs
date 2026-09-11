using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SenangRetails.Data.Sqlite.Database.Migrations;

[DbContext(typeof(LocalAppDbContext))]
[Migration("202608240002_NormalizeOnlineSaleConfirmation")]
public sealed class NormalizeOnlineSaleConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE OfflineCashSales
            SET Status = 1,
                IsOnlineVisibilityConfirmed = 1,
                LastErrorMessage = NULL,
                NextAttemptAtUtc = NULL
            WHERE ServerDocumentId IS NOT NULL
              AND TRIM(ServerDocumentId) <> ''
              AND ServerDisplayCode IS NOT NULL
              AND TRIM(ServerDisplayCode) <> '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
