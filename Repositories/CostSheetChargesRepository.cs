using Microsoft.Data.SqlClient;
using RepoDb;
using RepoDbApi.Models;

namespace RepoDbApi.Repositories;

public class CostSheetChargesRepository : ICostSheetChargesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CostSheetChargesRepository> _logger;

    public CostSheetChargesRepository(IConfiguration configuration, ILogger<CostSheetChargesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CargoMintCostSheetCharge>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoMintCostSheetCharge>(
                "SELECT * FROM [vw_CargoMint_CostSheetCharges] WHERE [CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_CargoMint_CostSheetCharges for CargoID: {CargoId}", cargoId);
            throw;
        }
    }

    public async Task<IEnumerable<CargoMintCostSheetCharge>> GetByCostSheetIdAsync(int costSheetId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CargoMintCostSheetCharge>(
                "SELECT * FROM [vw_CargoMint_CostSheetCharges] WHERE [CostSheetID] = @costSheetId",
                new { costSheetId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_CargoMint_CostSheetCharges for CostSheetID: {CostSheetId}", costSheetId);
            throw;
        }
    }
}    
