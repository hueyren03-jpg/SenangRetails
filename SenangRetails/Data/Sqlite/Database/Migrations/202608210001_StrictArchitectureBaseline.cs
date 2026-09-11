using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SenangRetails.Data.Sqlite.Database.Migrations;

[DbContext(typeof(LocalAppDbContext))]
[Migration("202608210001_StrictArchitectureBaseline")]
public sealed class StrictArchitectureBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS OfflineCashSales (
                LocalId TEXT NOT NULL CONSTRAINT PK_OfflineCashSales PRIMARY KEY,
                LocalDisplayCode TEXT NOT NULL,
                BranchId TEXT NOT NULL,
                FinancialDate TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                AccountName TEXT NOT NULL,
                TotalAmount TEXT NOT NULL,
                ItemCount INTEGER NOT NULL,
                OrderPayloadJson TEXT NOT NULL,
                PaymentLinesJson TEXT NOT NULL,
                Status INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                SyncedAtUtc TEXT NULL,
                ServerDocumentId TEXT NULL,
                ServerDisplayCode TEXT NULL,
                LastErrorMessage TEXT NULL,
                RetryCount INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_OfflineCashSales_Status ON OfflineCashSales (Status);
            CREATE INDEX IF NOT EXISTS IX_OfflineCashSales_FinancialDate ON OfflineCashSales (FinancialDate);
            CREATE INDEX IF NOT EXISTS IX_OfflineCashSales_BranchId ON OfflineCashSales (BranchId);
            CREATE INDEX IF NOT EXISTS IX_OfflineCashSales_CreatedAtUtc ON OfflineCashSales (CreatedAtUtc);
            CREATE TABLE IF NOT EXISTS LocalDataCaches (
                CacheKey TEXT NOT NULL CONSTRAINT PK_LocalDataCaches PRIMARY KEY,
                DataJson TEXT NOT NULL,
                LastUpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_LocalDataCaches_LastUpdatedAtUtc ON LocalDataCaches (LastUpdatedAtUtc);
            CREATE TABLE IF NOT EXISTS LocalItemCatalogs (
                BranchId TEXT NOT NULL CONSTRAINT PK_LocalItemCatalogs PRIMARY KEY,
                ItemsJson TEXT NOT NULL,
                CategoriesJson TEXT NOT NULL,
                PaymentMethodsJson TEXT NOT NULL,
                LastUpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS LocalCustomers (
                MasterAccountId TEXT NOT NULL CONSTRAINT PK_LocalCustomers PRIMARY KEY,
                AccountName TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Email TEXT NOT NULL,
                NRIC TEXT NOT NULL,
                MembershipTypeName TEXT NOT NULL,
                RawJson TEXT NOT NULL,
                LastUpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_LocalCustomers_AccountName ON LocalCustomers (AccountName);
            CREATE INDEX IF NOT EXISTS IX_LocalCustomers_Phone ON LocalCustomers (Phone);
            CREATE INDEX IF NOT EXISTS IX_LocalCustomers_NRIC ON LocalCustomers (NRIC);
            CREATE INDEX IF NOT EXISTS IX_LocalCustomers_Email ON LocalCustomers (Email);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OfflineCashSales");
        migrationBuilder.DropTable(name: "LocalDataCaches");
        migrationBuilder.DropTable(name: "LocalItemCatalogs");
        migrationBuilder.DropTable(name: "LocalCustomers");
    }
}
