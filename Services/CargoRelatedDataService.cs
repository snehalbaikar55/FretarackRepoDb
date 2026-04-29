using GeneratedModels;
using RepoDbApi.Models;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public class CargoRelatedDataService : ICargoRelatedDataService
{
    private readonly ICargoRelatedDataRepository _cargoRelatedDataRepository;
    private readonly ILogger<CargoRelatedDataService> _logger;

    public CargoRelatedDataService(
        ICargoRelatedDataRepository cargoRelatedDataRepository,
        ILogger<CargoRelatedDataService> logger)
    {
        _cargoRelatedDataRepository = cargoRelatedDataRepository;
        _logger = logger;
    }

    public async Task<CargoRelatedDataResponse> GetByCargoIdAsync(int cargoId)
    {
        try
        {
            return await _cargoRelatedDataRepository.GetByCargoIdAsync(cargoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving related cargo data in service for CargoID: {CargoId}", cargoId);
            throw;
        }
    }

    public async Task<IEnumerable<Invoices>> GetInvoicesAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetInvoicesAsync(cargoId);
    }

    public async Task<IEnumerable<VendorBill>> GetVendorBillsAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetVendorBillsAsync(cargoId);
    }

    public async Task<IEnumerable<CostSheet>> GetCostSheetsAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetCostSheetsAsync(cargoId);
    }

    public async Task<IEnumerable<CargoCharges>> GetCargoChargesAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetCargoChargesAsync(cargoId);
    }

    public async Task<IEnumerable<CargoContainers>> GetCargoContainersAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetCargoContainersAsync(cargoId);
    }

    public async Task<IEnumerable<CargoPackages>> GetCargoPackagesAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetCargoPackagesAsync(cargoId);
    }

    public async Task<IEnumerable<CompanyAddress>> GetCompanyAddressesAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetCompanyAddressesAsync(cargoId);
    }

    public async Task<IEnumerable<AirShipmentRouting>> GetAirShipmentRoutingsAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetAirShipmentRoutingsAsync(cargoId);
    }

    public async Task<IEnumerable<OceanShipmentRouting>> GetOceanShipmentRoutingsAsync(int cargoId)
    {
        return await _cargoRelatedDataRepository.GetOceanShipmentRoutingsAsync(cargoId);
    }
}
