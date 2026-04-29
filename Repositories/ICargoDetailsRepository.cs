using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoDetailsRepository
{
    Task<IEnumerable<CargoDetails>> GetByCargoIdAsync(int cargoId);
}
