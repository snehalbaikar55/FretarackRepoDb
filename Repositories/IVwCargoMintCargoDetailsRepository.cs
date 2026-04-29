using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IVwCargoMintCargoDetailsRepository
{
    Task<IEnumerable<vw_CargoMint_CargoDetails>> GetByCargoIdAsync(int cargoId);
}
