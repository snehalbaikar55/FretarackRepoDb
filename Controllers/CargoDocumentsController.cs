using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;
using System.Net;
using System.IO;
using FluentFTP;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/cargo-documents")]
public class CargoDocumentsController : ControllerBase
{
    private readonly ICargoDocumentsService _service;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CargoDocumentsController> _logger;

     private readonly string? _ftpUserName;
    private readonly string? _ftpPassword;

    public CargoDocumentsController(ICargoDocumentsService service, IConfiguration configuration, ILogger<CargoDocumentsController> logger)
    {
        _service = service;
        _configuration = configuration;
        _logger = logger;
        _ftpUserName = configuration["Ftp:UserName"];
        _ftpPassword = configuration["Ftp:Password"];
       logger.LogInformation("FTP Username Passed: {Username}", _ftpUserName);

logger.LogInformation(
    "Password Empty: {IsEmpty}",
    string.IsNullOrWhiteSpace(_ftpPassword) ? "Yes" : "No");
        
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }

    [HttpGet("{id:int}/view")]
    public async Task<IActionResult> ViewDocument([FromRoute] int id)
    {
        var doc = await _service.GetByIdAsync(id);
        if (doc is null)
            return NotFound();

        var ftpLink = doc.FTPLink ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ftpLink))
            return BadRequest("Document has no FTP link.");

        byte[]? fileBytes = null;
        string fileName = Path.GetFileName(ftpLink);
        string contentType = GetContentTypeFromExtension(Path.GetExtension(fileName));

        // Read optional FTP credentials from configuration
        var ftpUser = _configuration["Ftp:Username"];
        var ftpPass = _configuration["Ftp:Password"];

        // Try original link first, then attempt a simple host swap fallback
        var attempts = new[] { ftpLink, SwapHostFallback(ftpLink) }.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct();

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
                _logger.LogWarning(ex, "Failed to download FTP file from {Url}", url);
                // try next
            }
        }

        if (fileBytes == null || fileBytes.Length == 0)
            return StatusCode(502, "Unable to retrieve document from FTP server.");

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
        // Try passive first then active if passive fails
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
                // try next passive mode
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FTP download unexpected error for {Url} (passive={Passive})", ftpUrl, passive);
            }
        }

        return Array.Empty<byte>();
    }
}
