using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICostSheetsRepository
{
    Task<IEnumerable<CostSheet>> GetByCargoIdAsync(int cargoId);
}
