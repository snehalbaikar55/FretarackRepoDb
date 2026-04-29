using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class FundRequiremntService : IFundRequiremntService
{
    private readonly IFundRequiremntRepository _repository;

    public FundRequiremntService(IFundRequiremntRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<FundRequiremnt>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
