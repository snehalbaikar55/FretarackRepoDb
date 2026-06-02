using GeneratedModels;

namespace RepoDbApi.Services;

public interface IIncomeExpenseStatusService
{
    Task<IEnumerable<IncomeExpenseStatus>> GetByJobNoAsync(string jobNo);
}
