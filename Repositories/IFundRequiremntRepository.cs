using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IFundRequiremntRepository
{
    Task<IEnumerable<FundRequiremnt>> GetByCargoIdAsync(int cargoId);
}
