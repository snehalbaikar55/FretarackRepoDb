using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoContainersRepository
{
    Task<IEnumerable<CargoContainers>> GetByCargoIdAsync(int cargoId);
}
