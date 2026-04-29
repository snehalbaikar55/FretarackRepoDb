using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoAWBService
{
    Task<IEnumerable<CargoAWB>> GetByCargoIdAsync(int cargoId);
}
