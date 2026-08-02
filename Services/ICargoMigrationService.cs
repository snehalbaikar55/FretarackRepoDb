using RepoDbApi.Contracts;

namespace RepoDbApi.Services;

public interface ICargoMigrationService
{
    Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, CancellationToken cancellationToken = default);
}
