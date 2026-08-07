using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public interface ICargoDocumentMigrationRepository
{
    Task<CargoDocumentMigrationResult> MigrateAsync(int fretrackCargoDocumentId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateAsync(int cargoId, int fretrackCargoDocumentId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateByCargoIdAsync(int cargoId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateByCargoIdAsync(int cargoId, int orgId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateBulkAsync(CancellationToken cancellationToken = default);
}
