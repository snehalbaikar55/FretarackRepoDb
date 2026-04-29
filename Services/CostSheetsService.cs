using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CostSheetsService : ICostSheetsService
{
    private readonly ICostSheetsRepository _repository;

    public CostSheetsService(ICostSheetsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CostSheet>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
