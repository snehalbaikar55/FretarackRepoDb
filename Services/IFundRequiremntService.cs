using GeneratedModels;

namespace RepoDbApi.Services;

public interface IFundRequiremntService
{
    Task<IEnumerable<FundRequiremnt>> GetByCargoIdAsync(int cargoId);
}
