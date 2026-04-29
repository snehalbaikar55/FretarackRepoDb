using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoService : ICargoService
{
    private readonly ICargoRepository _cargoRepository;
    private readonly ILogger<CargoService> _logger;

    public CargoService(ICargoRepository cargoRepository, ILogger<CargoService> logger)
    {
        _cargoRepository = cargoRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Cargo>> GetAllCargoAsync()
    {
        try
        {
            return await _cargoRepository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cargo records in service.");
            throw;
        }
    }
}
