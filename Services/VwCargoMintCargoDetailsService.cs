using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class VwCargoMintCargoDetailsService : IVwCargoMintCargoDetailsService
{
    private readonly IVwCargoMintCargoDetailsRepository _repository;

    public VwCargoMintCargoDetailsService(IVwCargoMintCargoDetailsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<vw_CargoMint_CargoDetails>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
