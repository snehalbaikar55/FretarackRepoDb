using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoHBLService
{
    Task<IEnumerable<CargoHBL>> GetByCargoIdAsync(int cargoId);
}
