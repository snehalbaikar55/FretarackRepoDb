using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/invoice-line-items")]
public class InvoiceLineItemsController : ControllerBase
{
    private readonly IInvoiceLineItemsService _service;

    public InvoiceLineItemsController(IInvoiceLineItemsService service)
    {
        _service = service;
    }

    [HttpGet("by-invoice/{invoiceId:int}")]
    public async Task<IActionResult> GetByInvoiceId(int invoiceId)
    {
        return Ok(await _service.GetByInvoiceIdAsync(invoiceId));
    }
}
