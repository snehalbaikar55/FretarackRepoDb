using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class FundRequiremntRepository : IFundRequiremntRepository
{
    private readonly string _connectionString;
    private readonly ILogger<FundRequiremntRepository> _logger;

    public FundRequiremntRepository(IConfiguration configuration, ILogger<FundRequiremntRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<FundRequiremnt>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<FundRequiremnt>(
                "SELECT * FROM [FundRequiremnt] WHERE [CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving FundRequiremnt records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
