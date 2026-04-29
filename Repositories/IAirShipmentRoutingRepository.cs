using GeneratedModels;

namespace RepoDbApi.Repositories;

public interface IAirShipmentRoutingRepository
{
    Task<IEnumerable<AirShipmentRouting>> GetByCargoIdAsync(int cargoId);
}
