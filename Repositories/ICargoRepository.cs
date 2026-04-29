using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface ICargoRepository
{
    Task<IEnumerable<Cargo>> GetAllAsync();
}
