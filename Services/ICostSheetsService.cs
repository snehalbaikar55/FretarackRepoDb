using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICostSheetsService
{
    Task<IEnumerable<CostSheet>> GetByCargoIdAsync(int cargoId);
}
