using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoPackagesService
{
    Task<IEnumerable<CargoPackages>> GetByCargoIdAsync(int cargoId);
}
