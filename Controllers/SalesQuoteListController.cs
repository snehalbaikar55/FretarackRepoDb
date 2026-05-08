using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;
using System.IO;
using System.Net;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/sales-quote-list")]
public class SalesQuoteListController : ControllerBase
{
    private readonly ISalesQuoteListService _service;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SalesQuoteListController> _logger;

    public SalesQuoteListController(
        ISalesQuoteListService service,
        IConfiguration configuration,
        ILogger<SalesQuoteListController> logger)
    {
        _service = service;
        _configuration = configuration;
        _logger = logger;
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

    [HttpGet("{id:int}/emailcc/view")]
    public async Task<IActionResult> ViewEmailCcDocument([FromRoute] int id)
    {
        var quote = await _service.GetByIdAsync(id);
        if (quote is null)
            return NotFound();

        var ftpLink = quote.EmailCC ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ftpLink))
            return BadRequest("Sales quote EmailCC path is empty.");

        byte[]? fileBytes = null;
        var fileName = Path.GetFileName(ftpLink);
        var contentType = GetContentTypeFromExtension(Path.GetExtension(fileName));

        var ftpUser = _configuration["Ftp:Username"];
        var ftpPass = _configuration["Ftp:Password"];

        var attempts = new[] { ftpLink, SwapHostFallback(ftpLink) }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct();

        foreach (var url in attempts)
        {
            try
            {
                fileBytes = await DownloadFtpFileAsync(url, ftpUser, ftpPass, _logger);
                if (fileBytes != null && fileBytes.Length > 0)
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download EmailCC file from {Url}", url);
            }
        }

        if (fileBytes == null || fileBytes.Length == 0)
            return StatusCode(502, "Unable to retrieve EmailCC document from FTP server.");

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(fileBytes, contentType);
    }

    [HttpGet("emailcc/view")]
    public async Task<IActionResult> ViewEmailCcDocumentByPath([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("Query parameter 'path' is required.");

        byte[]? fileBytes = null;
        var ftpLink = path.Trim();
        var fileName = Path.GetFileName(ftpLink);
        var contentType = GetContentTypeFromExtension(Path.GetExtension(fileName));

        var ftpUser = _configuration["Ftp:Username"];
        var ftpPass = _configuration["Ftp:Password"];

        var attempts = new[] { ftpLink, SwapHostFallback(ftpLink) }
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct();

        foreach (var url in attempts)
        {
            try
            {
                fileBytes = await DownloadFtpFileAsync(url, ftpUser, ftpPass, _logger);
                if (fileBytes != null && fileBytes.Length > 0)
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download EmailCC file from {Url}", url);
            }
        }

        if (fileBytes == null || fileBytes.Length == 0)
            return StatusCode(502, "Unable to retrieve EmailCC document from FTP server.");

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(fileBytes, contentType);
    }

    private static string SwapHostFallback(string original)
    {
        if (string.IsNullOrWhiteSpace(original)) return original;

        var swapped = original;
        if (original.ToUpperInvariant().Contains("192.168.0.10") && !original.ToUpperInvariant().Contains("SETUP.FRETLOG.COM"))
            swapped = original.Replace("192.168.0.10", "SETUP.FRETLOG.COM");
        else if (original.ToUpperInvariant().Contains("SETUP.FRETLOG.COM") && !original.ToUpperInvariant().Contains("192.168.0.10"))
            swapped = original.Replace("SETUP.FRETLOG.COM", "192.168.0.10");

        return swapped;
    }

    private static string GetContentTypeFromExtension(string ext)
    {
        return ext?.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }

    private static async Task<byte[]> DownloadFtpFileAsync(string ftpUrl, string? username, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(ftpUrl))
            return Array.Empty<byte>();

        var uri = new Uri(ftpUrl);
        var passiveOptions = new[] { true, false };
        foreach (var passive in passiveOptions)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.UseBinary = true;
                request.UsePassive = passive;
                request.KeepAlive = false;

                if (!string.IsNullOrWhiteSpace(username))
                    request.Credentials = new NetworkCredential(username, password ?? string.Empty);

                using var response = (FtpWebResponse)request.GetResponse();
                using var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    continue;

                using var ms = new MemoryStream();
                responseStream.CopyTo(ms);
                logger.LogInformation("Downloaded {Bytes} bytes from {Url} (Passive={Passive})", ms.Length, ftpUrl, passive);
                return ms.ToArray();
            }
            catch (WebException wex)
            {
                logger.LogWarning(wex, "FTP download failed for {Url} with passive={Passive}. Status={Status}", ftpUrl, passive, wex.Status);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FTP download unexpected error for {Url} (passive={Passive})", ftpUrl, passive);
            }
        }

        return Array.Empty<byte>();
    }
}
