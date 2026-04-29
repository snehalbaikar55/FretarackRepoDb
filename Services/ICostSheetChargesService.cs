using RepoDbApi.Models;

namespace RepoDbApi.Services;

public interface ICostSheetChargesService
{
    Task<IEnumerable<CargoMintCostSheetCharge>> GetByCargoIdAsync(int cargoId);
    Task<IEnumerable<CargoMintCostSheetCharge>> GetByCostSheetIdAsync(int costSheetId);
}
