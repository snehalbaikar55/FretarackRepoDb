using RepoDbApi.Models;

namespace RepoDbApi.Repositories;

public interface ICostSheetChargesRepository
{
    Task<IEnumerable<CargoMintCostSheetCharge>> GetByCargoIdAsync(int cargoId);
    Task<IEnumerable<CargoMintCostSheetCharge>> GetByCostSheetIdAsync(int costSheetId);
}
