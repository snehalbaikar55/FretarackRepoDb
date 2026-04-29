using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class AllCargoListService : IAllCargoListService
{
    private readonly IAllCargoListRepository _allCargoListRepository;
    private readonly ILogger<AllCargoListService> _logger;

    public AllCargoListService(
        IAllCargoListRepository allCargoListRepository,
        ILogger<AllCargoListService> logger)
    {
        _allCargoListRepository = allCargoListRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<AllCargoList>> GetAllAsync(DateTime? createdFrom = null, DateTime? createdTo = null, string? job = null, string? cycle = null)
    {
        try
        {
            return await _allCargoListRepository.GetAllAsync(createdFrom, createdTo, job, cycle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving AllCargoList records in service.");
            throw;
        }
    }
}
