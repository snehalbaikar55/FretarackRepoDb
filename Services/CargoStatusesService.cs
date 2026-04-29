using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoStatusesService : ICargoStatusesService
{
    private readonly ICargoStatusesRepository _repository;

    public CargoStatusesService(ICargoStatusesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoStatuses>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
