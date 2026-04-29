using GeneratedModels;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class OceanShipmentRoutingService : IOceanShipmentRoutingService
{
    private readonly IOceanShipmentRoutingRepository _repository;

    public OceanShipmentRoutingService(IOceanShipmentRoutingRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<OceanShipmentRouting>> GetByCargoIdAsync(int cargoId)
    {
        return await _repository.GetByCargoIdAsync(cargoId);
    }
}
