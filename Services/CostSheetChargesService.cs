using RepoDbApi.Models;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CostSheetChargesService : ICostSheetChargesService
{
    private readonly ICostSheetChargesRepository _repository;
    private readonly ILogger<CostSheetChargesService> _logger;

    public CostSheetChargesService(ICostSheetChargesRepository repository, ILogger<CostSheetChargesService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<CargoMintCostSheetCharge>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            return await _repository.GetByCargoIdAsync(cargoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cost sheet charges for CargoID: {CargoId}", cargoId);
            throw;
        }
    }

    public async Task<IEnumerable<CargoMintCostSheetCharge>> GetByCostSheetIdAsync(int costSheetId)
    {
        try
        {
            return await _repository.GetByCostSheetIdAsync(costSheetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving cost sheet charges for CostSheetID: {CostSheetId}", costSheetId);
            throw;
        }
    }
}
