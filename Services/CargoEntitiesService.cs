using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoEntitiesService : ICargoEntitiesService
{
    private readonly ICargoEntitiesRepository _repository;

    public CargoEntitiesService(ICargoEntitiesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoEntities>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
