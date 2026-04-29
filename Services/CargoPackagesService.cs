using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoPackagesService : ICargoPackagesService
{
    private readonly ICargoPackagesRepository _repository;

    public CargoPackagesService(ICargoPackagesRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CargoPackages>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
