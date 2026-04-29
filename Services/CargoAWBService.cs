using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoAWBService : ICargoAWBService
{
    private readonly ICargoAWBRepository _repository;

    public CargoAWBService(ICargoAWBRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoAWB>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
