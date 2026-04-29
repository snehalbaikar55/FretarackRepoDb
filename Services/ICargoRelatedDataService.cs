using GeneratedModels;
using RepoDbApi.Models;

namespace RepoDbApi.Services;

public interface ICargoRelatedDataService
{
    Task<CargoRelatedDataResponse> GetByCargoIdAsync(int cargoId);
    Task<IEnumerable<Invoices>> GetInvoicesAsync(int cargoId);
    Task<IEnumerable<VendorBill>> GetVendorBillsAsync(int cargoId);
    Task<IEnumerable<CostSheet>> GetCostSheetsAsync(int cargoId);
    Task<IEnumerable<CargoCharges>> GetCargoChargesAsync(int cargoId);
    Task<IEnumerable<CargoContainers>> GetCargoContainersAsync(int cargoId);
    Task<IEnumerable<CargoPackages>> GetCargoPackagesAsync(int cargoId);
    Task<IEnumerable<CompanyAddress>> GetCompanyAddressesAsync(int cargoId);
    Task<IEnumerable<AirShipmentRouting>> GetAirShipmentRoutingsAsync(int cargoId);
    Task<IEnumerable<OceanShipmentRouting>> GetOceanShipmentRoutingsAsync(int cargoId);
}
