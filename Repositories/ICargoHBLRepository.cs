using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoHBLRepository
{
    Task<IEnumerable<CargoHBL>> GetByCargoIdAsync(int cargoId);
}
