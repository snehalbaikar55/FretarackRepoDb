using RepoDbApi.Contracts;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoMigrationService : ICargoMigrationService
{
    private readonly ICargoMigrationRepository _repository;
    private readonly ILogger<CargoMigrationService> _logger;

    public CargoMigrationService(ICargoMigrationRepository repository, ILogger<CargoMigrationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, int orgId = 18, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateSingleJobAsync(jobNo, orgId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while migrating single cargo for JobNo: {JobNo}", jobNo);
            throw;
        }
    }

    public async Task<CargoMigrationResponse> MigrateContainerOnlyAsync(string jobNo, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateContainerOnlyAsync(jobNo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while staging container only for JobNo: {JobNo}", jobNo);
            throw;
        }
    }
}
