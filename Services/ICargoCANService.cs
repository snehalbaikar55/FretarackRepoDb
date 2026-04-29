using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoCANService
{
    Task<IEnumerable<CargoCAN>> GetByCargoIdAsync(int cargoId);
}
