using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoEntitiesService
{
    Task<IEnumerable<CargoEntities>> GetByCargoIdAsync(int cargoId);
}
