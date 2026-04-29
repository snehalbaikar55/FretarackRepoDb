using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoHBLService : ICargoHBLService
{
    private readonly ICargoHBLRepository _repository;

    public CargoHBLService(ICargoHBLRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoHBL>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
