using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public interface ICargoMigrationRepository
{
    Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, CancellationToken cancellationToken = default);
}
