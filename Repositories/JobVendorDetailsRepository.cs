using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class JobVendorDetailsRepository : IJobVendorDetailsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<JobVendorDetailsRepository> _logger;

    public JobVendorDetailsRepository(IConfiguration configuration, ILogger<JobVendorDetailsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<JobVendorDetails>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<JobVendorDetails>(
                "SELECT * FROM [JobVendorDetails] WHERE [CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving JobVendorDetails records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
