using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/sales-quote-list")]
public class SalesQuoteListController : ControllerBase
{
    private readonly ISalesQuoteListService _service;

    public SalesQuoteListController(ISalesQuoteListService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? salesQuoteNumber = null,
        [FromQuery] string? quoteType = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? contact = null,
        [FromQuery] string? salesPerson = null,
        [FromQuery] string? salesQuoteStatus = null)
    {
        return Ok(await _service.GetAllAsync(
            fromDate,
            toDate,
            salesQuoteNumber,
            quoteType,
            customer,
            contact,
            salesPerson,
            salesQuoteStatus));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("{id:int}/details")]
    public async Task<IActionResult> GetDetails([FromRoute] int id)
    {
        var results = await _service.GetDetailsBySalesQuoteIdAsync(id);
        return Ok(results);
    }

    [HttpGet("{id:int}/charges/income")]
    public async Task<IActionResult> GetIncomeCharges([FromRoute] int id)
    {
        var results = await _service.GetIncomeChargesBySalesQuoteIdAsync(id);
        return Ok(results);
    }
}
