using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Data.Sqlite.Database;
using SenangRetails.Data.Sqlite.Database.Entities;

namespace SenangRetails.Data.Sqlite.Customers;

internal sealed class LocalCustomerRepository(IDbContextFactory<LocalAppDbContext> contextFactory)
    : ILocalCustomerRepository
{
    public async Task<IReadOnlyList<LocalCustomerRecord>> SearchAsync(
        string keyword,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.LocalCustomers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.AccountName, pattern) ||
                EF.Functions.Like(x.Phone, pattern) ||
                EF.Functions.Like(x.MasterAccountId, pattern) ||
                EF.Functions.Like(x.Nric, pattern) ||
                EF.Functions.Like(x.Email, pattern));
        }
        var rows = await query.OrderBy(x => x.AccountName).Take(limit).ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<LocalCustomerRecord?> GetAsync(string idOrPhone, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var key = idOrPhone.Trim();
        var row = await db.LocalCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MasterAccountId == key || x.Phone == key, cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    public async Task UpsertAsync(IEnumerable<LocalCustomerRecord> customers, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var customer in customers.Where(x => !string.IsNullOrWhiteSpace(x.MasterAccountId)))
        {
            var row = await db.LocalCustomers.FirstOrDefaultAsync(
                x => x.MasterAccountId == customer.MasterAccountId, cancellationToken);
            if (row is null)
            {
                row = new LocalCustomerEntity { MasterAccountId = customer.MasterAccountId };
                db.LocalCustomers.Add(row);
            }
            row.AccountName = customer.AccountName;
            row.Phone = customer.Phone;
            row.Email = customer.Email;
            row.Nric = customer.Nric;
            row.MembershipTypeName = customer.MembershipTypeName;
            row.RawJson = customer.RawJson;
            row.LastUpdatedAtUtc = customer.LastUpdatedAtUtc;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static LocalCustomerRecord ToRecord(LocalCustomerEntity row) => new(
        row.MasterAccountId, row.AccountName, row.Phone, row.Email, row.Nric,
        row.MembershipTypeName, row.RawJson, row.LastUpdatedAtUtc);
}
