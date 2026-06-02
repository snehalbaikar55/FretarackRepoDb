using Microsoft.AspNetCore.Mvc;
using RepoDbApi.Services;
using System.IO;
using System.Net;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/vendor-bills")]
public class VendorBillsController : ControllerBase
{
    private readonly IVendorBillsService _service;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VendorBillsController> _logger;

    public VendorBillsController(
        IVendorBillsService service,
        IConfiguration configuration,
        ILogger<VendorBillsController> logger)
    {
        _service = service;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("by-cargo/{cargoId:int}")]
    public async Task<IActionResult> GetByCargoId(int cargoId)
    {
        return Ok(await _service.GetByCargoIdAsync(cargoId));
    }

    [HttpGet("view")]
    public async Task<IActionResult> ViewVendorBillDocumentByPath([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("Query parameter 'path' is required.");

        byte[]? fileBytes = null;
        var ftpLink = path.Trim().Trim('\'', '"');
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
                _logger.LogWarning(ex, "Failed to download Vendor Bill file from {Url}", url);
            }
        }

        if (fileBytes == null || fileBytes.Length == 0)
            return StatusCode(502, "Unable to retrieve Vendor Bill document from FTP server.");

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

        var normalizedFtpUrl = NormalizeFtpUrl(ftpUrl);
        Uri uri;
        try
        {
            uri = new Uri(normalizedFtpUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid FTP URL. Original={OriginalUrl}, Normalized={NormalizedUrl}", ftpUrl, normalizedFtpUrl);
            return Array.Empty<byte>();
        }

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
                logger.LogInformation("Downloaded {Bytes} bytes from {Url} (Passive={Passive})", ms.Length, normalizedFtpUrl, passive);
                return ms.ToArray();
            }
            catch (WebException wex)
            {
                logger.LogWarning(wex, "FTP download failed for {Url} (Original={OriginalUrl}) with passive={Passive}. Status={Status}", normalizedFtpUrl, ftpUrl, passive, wex.Status);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FTP download unexpected error for {Url} (Original={OriginalUrl}, passive={Passive})", normalizedFtpUrl, ftpUrl, passive);
            }
        }

        return Array.Empty<byte>();
    }

    private static string NormalizeFtpUrl(string ftpUrl)
    {
        if (string.IsNullOrWhiteSpace(ftpUrl))
            return ftpUrl;

        var input = ftpUrl.Trim().Trim('\'', '"').Replace("\\", "/");
        const string ftpPrefix = "ftp://";
        if (!input.StartsWith(ftpPrefix, StringComparison.OrdinalIgnoreCase))
            return input;

        var withoutScheme = input.Substring(ftpPrefix.Length);
        var firstSlash = withoutScheme.IndexOf('/');
        if (firstSlash < 0)
            return ftpPrefix + withoutScheme;

        var host = withoutScheme.Substring(0, firstSlash);
        var path = withoutScheme.Substring(firstSlash);

        while (path.Contains("//", StringComparison.Ordinal))
            path = path.Replace("//", "/", StringComparison.Ordinal);

        return ftpPrefix + host + path;
    }
}
