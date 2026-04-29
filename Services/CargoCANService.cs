using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoCANService : ICargoCANService
{
    private readonly ICargoCANRepository _repository;

    public CargoCANService(ICargoCANRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoCAN>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
