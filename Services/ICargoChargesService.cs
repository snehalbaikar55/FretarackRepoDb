using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoChargesService
{
    Task<IEnumerable<CargoCharges>> GetByCargoIdAsync(int cargoId);
}
