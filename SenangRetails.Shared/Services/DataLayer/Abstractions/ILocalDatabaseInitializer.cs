namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public interface ILocalDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
