using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class CompanyAddressesRepository : ICompanyAddressesRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CompanyAddressesRepository> _logger;

    public CompanyAddressesRepository(IConfiguration configuration, ILogger<CompanyAddressesRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<CompanyAddress>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<CompanyAddress>(
                "SELECT * FROM [CompanyAddress] WHERE [CompanyID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving company addresses for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
