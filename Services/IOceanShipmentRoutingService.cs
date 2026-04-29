using GeneratedModels;

namespace RepoDbApi.Services;

public interface IOceanShipmentRoutingService
{
    Task<IEnumerable<OceanShipmentRouting>> GetByCargoIdAsync(int cargoId);
}
