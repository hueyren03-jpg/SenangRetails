namespace SenangRetails.Data.Sqlite.Database.Entities;

internal sealed class LocalCustomerEntity
{
    public string MasterAccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nric { get; set; } = string.Empty;
    public string MembershipTypeName { get; set; } = string.Empty;
    public string RawJson { get; set; } = "{}";
    public DateTime LastUpdatedAtUtc { get; set; }
}
