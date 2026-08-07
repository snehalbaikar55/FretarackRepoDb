using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public interface ICargoMigrationRepository
{
    Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, int orgId = 18, CancellationToken cancellationToken = default);

    Task<CargoMigrationResponse> MigrateContainerOnlyAsync(string jobNo, CancellationToken cancellationToken = default);
}
