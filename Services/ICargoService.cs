using GeneratedModels;

namespace RepoDbApi.Services;

public interface ICargoService
{
    Task<IEnumerable<Cargo>> GetAllCargoAsync();
}
