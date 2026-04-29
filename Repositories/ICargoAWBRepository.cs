using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoAWBRepository
{
    Task<IEnumerable<CargoAWB>> GetByCargoIdAsync(int cargoId);
}
