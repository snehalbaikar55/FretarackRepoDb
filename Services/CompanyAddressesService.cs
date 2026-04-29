using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CompanyAddressesService : ICompanyAddressesService
{
    private readonly ICompanyAddressesRepository _repository;

    public CompanyAddressesService(ICompanyAddressesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CompanyAddress>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
