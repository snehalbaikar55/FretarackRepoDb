using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class IncomeExpenseStatusService : IIncomeExpenseStatusService
{
    private readonly IIncomeExpenseStatusRepository _repository;
    private readonly ILogger<IncomeExpenseStatusService> _logger;

    public IncomeExpenseStatusService(
        IIncomeExpenseStatusRepository repository,
        ILogger<IncomeExpenseStatusService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<IncomeExpenseStatus>> GetByJobNoAsync(string jobNo)
    {
        try
        {
            return await _repository.GetByJobNoAsync(jobNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving IncomeExpenseStatus in service for JobNo {JobNo}.", jobNo);
            throw;
        }
    }
}
