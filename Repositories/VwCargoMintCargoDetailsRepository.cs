using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class VwCargoMintCargoDetailsRepository : IVwCargoMintCargoDetailsRepository
{
    private readonly string _connectionString;
    private readonly ILogger<VwCargoMintCargoDetailsRepository> _logger;

    public VwCargoMintCargoDetailsRepository(IConfiguration configuration, ILogger<VwCargoMintCargoDetailsRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<vw_CargoMint_CargoDetails>> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<vw_CargoMint_CargoDetails>(
                @"SELECT
                        v.InvoiceNumbers,
                        v.shipperInvoiceDetails,
                        v.OverseasAgent,
                        v.JobNo,
                        v.DateCreated,
                        v.JobType,
                        v.isGstJob,
                        v.ModeOfTransport,
                        v.TransportDirection,
                        v.TypeOfMove,
                        v.POL,
                        v.POD,
                        v.etd,
                        v.ETA,
                        v.IncotermName,
                        v.FreightStatus,
                        v.[Job Handle By] AS JobHandleBy,
                        v.[Place Of Receipt] AS PlaceOfReceipt,
                        v.CarrierName
                  FROM [vw_CargoMint_CargoDetails] v
                  JOIN [cargo] c ON c.[JobNo] = v.[JobNo]
                  WHERE c.[CargoID] = @cargoId",
                new { cargoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving vw_CargoMint_CargoDetails records for CargoID: {CargoId}", cargoId);
            throw;
        }
    }
}
