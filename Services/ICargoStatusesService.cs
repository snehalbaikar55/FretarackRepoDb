using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoStatusesService
{
    Task<IEnumerable<CargoStatuses>> GetByCargoIdAsync(int cargoId);
}
