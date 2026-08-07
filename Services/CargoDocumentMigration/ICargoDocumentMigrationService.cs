using RepoDbApi.Contracts;

namespace RepoDbApi.Services.CargoDocumentMigration;

public interface ICargoDocumentMigrationService
{
    Task<CargoDocumentMigrationResult> MigrateAsync(int fretrackCargoDocumentId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateAsync(int cargoId, int fretrackCargoDocumentId, CancellationToken cancellationToken = default);

    Task<CargoDocumentMigrationResult> MigrateBulkAsync(CancellationToken cancellationToken = default);
}
