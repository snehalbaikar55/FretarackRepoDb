using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IOceanShipmentRoutingRepository
{
    Task<IEnumerable<OceanShipmentRouting>> GetByCargoIdAsync(int cargoId);
}
