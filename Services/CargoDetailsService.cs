using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoDetailsService : ICargoDetailsService
{
    private readonly ICargoDetailsRepository _repository;

    public CargoDetailsService(ICargoDetailsRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoDetails>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
