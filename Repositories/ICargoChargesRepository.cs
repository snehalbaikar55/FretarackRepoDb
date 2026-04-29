using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoChargesRepository
{
    Task<IEnumerable<CargoCharges>> GetByCargoIdAsync(int cargoId);
}
