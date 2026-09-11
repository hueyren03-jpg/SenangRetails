namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public sealed record LocalCustomerRecord(
    string MasterAccountId,
    string AccountName,
    string Phone,
    string Email,
    string Nric,
    string MembershipTypeName,
    string RawJson,
    DateTime LastUpdatedAtUtc);

public interface ILocalCustomerRepository
{
    Task<IReadOnlyList<LocalCustomerRecord>> SearchAsync(string keyword, int limit = 100, CancellationToken cancellationToken = default);
    Task<LocalCustomerRecord?> GetAsync(string idOrPhone, CancellationToken cancellationToken = default);
    Task UpsertAsync(IEnumerable<LocalCustomerRecord> customers, CancellationToken cancellationToken = default);
}
