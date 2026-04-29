using GeneratedModels;

namespace RepoDbApi.Services;

public interface IAllCargoListService
{
    Task<IEnumerable<AllCargoList>> GetAllAsync(DateTime? createdFrom = null, DateTime? createdTo = null, string? job = null, string? cycle = null);
}
