using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoCANRepository
{
    Task<IEnumerable<CargoCAN>> GetByCargoIdAsync(int cargoId);
}
