using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;
using RepoDbApi.Models;

namespace RepoDbApi.Repositories;

public class CargoRelatedDataRepository : ICargoRelatedDataRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CargoRelatedDataRepository> _logger;

    public CargoRelatedDataRepository(IConfiguration configuration, ILogger<CargoRelatedDataRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<CargoRelatedDataResponse> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            return new CargoRelatedDataResponse
            {
                Invoices = await GetInvoicesAsync(cargoId),
                VendorBills = await GetVendorBillsAsync(cargoId),
                CostSheets = await GetCostSheetsAsync(cargoId),
                CargoCharges = await GetCargoChargesAsync(cargoId),
                CargoContainers = await GetCargoContainersAsync(cargoId),
                CargoPackages = await GetCargoPackagesAsync(cargoId),
                CompanyAddresses = await GetCompanyAddressesAsync(cargoId),
                AirShipmentRoutings = await GetAirShipmentRoutingsAsync(cargoId),
                OceanShipmentRoutings = await GetOceanShipmentRoutingsAsync(cargoId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving related cargo data for CargoID: {CargoId}", cargoId);
            throw;
        }
    }

    public async Task<IEnumerable<Invoices>> GetInvoicesAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<Invoices>(
            "SELECT * FROM [Invoices] WHERE [CargoID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<VendorBill>> GetVendorBillsAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<VendorBill>(
            "SELECT * FROM [VendorBill] WHERE [CargoID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<CostSheet>> GetCostSheetsAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<CostSheet>(
            "SELECT * FROM [CostSheet] WHERE [CargoID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<CargoCharges>> GetCargoChargesAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<CargoCharges>(
            "SELECT * FROM [CargoCharges] WHERE [CostSheetID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<CargoContainers>> GetCargoContainersAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<CargoContainers>(
            "SELECT * FROM [CargoContainers] WHERE [CargoID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<CargoPackages>> GetCargoPackagesAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<CargoPackages>(
            "SELECT * FROM [CargoPackages] WHERE [CargoID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<CompanyAddress>> GetCompanyAddressesAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<CompanyAddress>(
            "SELECT * FROM [CompanyAddress] WHERE [CompanyID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<AirShipmentRouting>> GetAirShipmentRoutingsAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<AirShipmentRouting>(
            "SELECT * FROM [AirShipmentRouting] WHERE [AirRoutingID] = @cargoId",
            new { cargoId });
    }

    public async Task<IEnumerable<OceanShipmentRouting>> GetOceanShipmentRoutingsAsync(int cargoId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteQueryAsync<OceanShipmentRouting>(
            "SELECT * FROM [OceanShipmentRouting] WHERE [OceanRoutingID] = @cargoId",
            new { cargoId });
    }
}
