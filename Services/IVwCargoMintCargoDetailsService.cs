using GeneratedModels;

namespace RepoDbApi.Services;

public interface IVwCargoMintCargoDetailsService
{
    Task<IEnumerable<vw_CargoMint_CargoDetails>> GetByCargoIdAsync(int cargoId);
}
