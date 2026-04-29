using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICompanyAddressesRepository
{
    Task<IEnumerable<CompanyAddress>> GetByCargoIdAsync(int cargoId);
}
