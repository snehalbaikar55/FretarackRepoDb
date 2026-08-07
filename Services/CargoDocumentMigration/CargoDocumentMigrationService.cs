using RepoDbApi.Contracts;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services.CargoDocumentMigration;

public sealed class CargoDocumentMigrationService : ICargoDocumentMigrationService
{
    private readonly ICargoDocumentMigrationRepository _repository;
    private readonly ILogger<CargoDocumentMigrationService> _logger;

    public CargoDocumentMigrationService(
        ICargoDocumentMigrationRepository repository,
        ILogger<CargoDocumentMigrationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CargoDocumentMigrationResult> MigrateAsync(int fretrackCargoDocumentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateAsync(fretrackCargoDocumentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while migrating cargo document {FretrackCargoDocumentId}.", fretrackCargoDocumentId);
            throw;
        }
    }

    public async Task<CargoDocumentMigrationResult> MigrateAsync(int cargoId, int fretrackCargoDocumentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateAsync(cargoId, fretrackCargoDocumentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while migrating cargo document {FretrackCargoDocumentId} for CargoID {CargoId}.",
                fretrackCargoDocumentId,
                cargoId);
            throw;
        }
    }

    public async Task<CargoDocumentMigrationResult> MigrateBulkAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateBulkAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while bulk migrating cargo documents.");
            throw;
        }
    }
}
