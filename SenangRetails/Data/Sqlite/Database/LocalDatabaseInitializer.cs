using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Services.DataLayer.Abstractions;

namespace SenangRetails.Data.Sqlite.Database;

internal sealed class LocalDatabaseInitializer(IDbContextFactory<LocalAppDbContext> contextFactory)
    : ILocalDatabaseInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var migrations = db.Database.GetMigrations().ToArray();
            if (migrations.Length == 0)
                throw new InvalidOperationException("No EF Core SQLite migrations were discovered.");

            Console.WriteLine($"[SQLite] Database: {db.Database.GetDbConnection().DataSource}");
            Console.WriteLine($"[SQLite] Discovered migrations: {string.Join(", ", migrations)}");
            await db.Database.MigrateAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
