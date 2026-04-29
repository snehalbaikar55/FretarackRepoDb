using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoStatusesRepository
{
    Task<IEnumerable<CargoStatuses>> GetByCargoIdAsync(int cargoId);
}
