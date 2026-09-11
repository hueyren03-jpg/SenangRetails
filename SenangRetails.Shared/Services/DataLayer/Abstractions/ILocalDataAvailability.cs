namespace SenangRetails.Shared.Services.DataLayer.Abstractions;

public interface ILocalDataAvailability
{
    bool HasPersistentStore { get; }
}

public sealed record LocalDataAvailability(bool HasPersistentStore) : ILocalDataAvailability;
