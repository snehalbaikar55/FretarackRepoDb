using GeneratedModels;

namespace RepoDbApi.Models;

public class CargoRelatedDataResponse
{
    public IEnumerable<Invoices> Invoices { get; set; } = [];
    public IEnumerable<VendorBill> VendorBills { get; set; } = [];
    public IEnumerable<CostSheet> CostSheets { get; set; } = [];
    public IEnumerable<CargoCharges> CargoCharges { get; set; } = [];
    public IEnumerable<CargoContainers> CargoContainers { get; set; } = [];
    public IEnumerable<CargoPackages> CargoPackages { get; set; } = [];
    public IEnumerable<CompanyAddress> CompanyAddresses { get; set; } = [];
    public IEnumerable<AirShipmentRouting> AirShipmentRoutings { get; set; } = [];
    public IEnumerable<OceanShipmentRouting> OceanShipmentRoutings { get; set; } = [];
}
