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

    public async Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.MigrateSingleJobAsync(jobNo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while migrating single cargo for JobNo: {JobNo}", jobNo);
            throw;
        }
    }
}
