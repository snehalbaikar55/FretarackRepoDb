using GeneratedModels;

namespace RepoDbApi.Services;

public interface IAirShipmentRoutingService
{
    Task<IEnumerable<AirShipmentRouting>> GetByCargoIdAsync(int cargoId);
}
