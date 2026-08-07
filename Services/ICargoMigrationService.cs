using RepoDbApi.Contracts;

namespace RepoDbApi.Services;

public interface ICargoMigrationService
{
    Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, int orgId = 18, CancellationToken cancellationToken = default);

    Task<CargoMigrationResponse> MigrateContainerOnlyAsync(string jobNo, CancellationToken cancellationToken = default);
}
