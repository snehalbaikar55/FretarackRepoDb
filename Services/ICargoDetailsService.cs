using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoDetailsService
{
    Task<IEnumerable<CargoDetails>> GetByCargoIdAsync(int cargoId);
}
