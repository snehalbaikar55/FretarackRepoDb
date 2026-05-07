using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/sales-quote-charges")]
public class SalesQuoteChargesController : ControllerBase
{
    private readonly ISalesQuoteChargesService _service;

    public SalesQuoteChargesController(ISalesQuoteChargesService service)
    {
        _service = service;
    }

    [HttpGet("by-salesquote/{salesQuoteId:int}")]
    public async Task<IActionResult> GetBySalesQuoteId(int salesQuoteId)
    {
        return Ok(await _service.GetBySalesQuoteIdAsync(salesQuoteId));
    }
}
