using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/vendor-bill-line-items")]
public class VendorBillLineItemsController : ControllerBase
{
    private readonly IVendorBillLineItemsService _service;

    public VendorBillLineItemsController(IVendorBillLineItemsService service)
    {
        _service = service;
    }

    [HttpGet("by-vendor-bill/{vendorBillId:int}")]
    public async Task<IActionResult> GetByVendorBillId(int vendorBillId)
    {
        return Ok(await _service.GetByVendorBillIdAsync(vendorBillId));
    }
}
