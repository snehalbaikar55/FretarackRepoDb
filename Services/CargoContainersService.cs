using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoContainersService : ICargoContainersService
{
    private readonly ICargoContainersRepository _repository;

    public CargoContainersService(ICargoContainersRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoContainers>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
