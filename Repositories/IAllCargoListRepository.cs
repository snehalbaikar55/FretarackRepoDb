using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IAllCargoListRepository
{
    Task<IEnumerable<AllCargoList>> GetAllAsync(DateTime? createdFrom = null, DateTime? createdTo = null, string? job = null, string? cycle = null);
}
