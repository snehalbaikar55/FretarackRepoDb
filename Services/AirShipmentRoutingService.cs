using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class AirShipmentRoutingService : IAirShipmentRoutingService
{
    private readonly IAirShipmentRoutingRepository _repository;

    public AirShipmentRoutingService(IAirShipmentRoutingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<AirShipmentRouting>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
