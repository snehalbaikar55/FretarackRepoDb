using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoContainersService
{
    Task<IEnumerable<CargoContainers>> GetByCargoIdAsync(int cargoId);
}
