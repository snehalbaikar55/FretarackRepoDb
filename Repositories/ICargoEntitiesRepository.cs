using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoEntitiesRepository
{
    Task<IEnumerable<CargoEntities>> GetByCargoIdAsync(int cargoId);
}
