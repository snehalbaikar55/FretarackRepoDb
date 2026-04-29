using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-related-data")]
public class CargoRelatedDataController : ControllerBase
{
    private readonly ICargoRelatedDataService _cargoRelatedDataService;
    private readonly ILogger<CargoRelatedDataController> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public CargoRelatedDataController(
        ICargoRelatedDataService cargoRelatedDataService,
        ILogger<CargoRelatedDataController> logger,
        IHostEnvironment hostEnvironment)
    {
        _cargoRelatedDataService = cargoRelatedDataService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet("{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetByCargoIdAsync(cargoId),
            "Failed to fetch related cargo data for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/invoices")]
    public async Task<IActionResult> GetInvoices(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetInvoicesAsync(cargoId),
            "Failed to fetch invoices for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/vendor-bills")]
    public async Task<IActionResult> GetVendorBills(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetVendorBillsAsync(cargoId),
            "Failed to fetch vendor bills for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/cost-sheets")]
    public async Task<IActionResult> GetCostSheets(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetCostSheetsAsync(cargoId),
            "Failed to fetch cost sheets for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/cargo-charges")]
    public async Task<IActionResult> GetCargoCharges(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetCargoChargesAsync(cargoId),
            "Failed to fetch cargo charges for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/cargo-containers")]
    public async Task<IActionResult> GetCargoContainers(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetCargoContainersAsync(cargoId),
            "Failed to fetch cargo containers for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/cargo-packages")]
    public async Task<IActionResult> GetCargoPackages(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetCargoPackagesAsync(cargoId),
            "Failed to fetch cargo packages for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/company-addresses")]
    public async Task<IActionResult> GetCompanyAddresses(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetCompanyAddressesAsync(cargoId),
            "Failed to fetch company addresses for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/air-shipment-routing")]
    public async Task<IActionResult> GetAirShipmentRouting(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetAirShipmentRoutingsAsync(cargoId),
            "Failed to fetch air shipment routing for CargoID: {CargoId}");
    }

    [HttpGet("{cargoId:int}/ocean-shipment-routing")]
    public async Task<IActionResult> GetOceanShipmentRouting(int cargoId)
    {
        return await Execute(cargoId, () => _cargoRelatedDataService.GetOceanShipmentRoutingsAsync(cargoId),
            "Failed to fetch ocean shipment routing for CargoID: {CargoId}");
    }

    private async Task<IActionResult> Execute<T>(
        int cargoId,
        Func<Task<T>> operation,
        string logMessage)
    {
        try
        {
            var result = await operation();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, logMessage, cargoId);
            if (_hostEnvironment.IsDevelopment())
            {
                return Problem(
                    title: "Failed to fetch cargo related data.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while fetching cargo related data.");
        }
    }
}
