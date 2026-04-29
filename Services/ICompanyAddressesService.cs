using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICompanyAddressesService
{
    Task<IEnumerable<CompanyAddress>> GetByCargoIdAsync(int cargoId);
}
