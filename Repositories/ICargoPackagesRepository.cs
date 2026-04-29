using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoPackagesRepository
{
    Task<IEnumerable<CargoPackages>> GetByCargoIdAsync(int cargoId);
}
