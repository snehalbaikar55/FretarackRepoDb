using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IIncomeExpenseStatusRepository
{
    Task<IEnumerable<IncomeExpenseStatus>> GetByJobNoAsync(string jobNo);
}
