using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RepoDbApi.Services;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RepoDbApi.Controllers;

[ApiController]
[Route("api/vendor-bills")]
public class VendorBillsController : ControllerBase
{
    private const long ZohoOrganizationId = 60015941827;
    private const int ZohoRequestsPerMinuteLimit = 100;
    private static readonly ConcurrentDictionary<long, SlidingWindowRequestLimiter> ZohoRequestLimiters = new();

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

    [HttpGet("zoho/bills")]
    public async Task<IActionResult> GetBillsFromZoho([FromQuery] int perPage = 200, CancellationToken cancellationToken = default)
    {
        perPage = perPage <= 0 ? 200 : Math.Min(perPage, 200);

        var zohoToken = await GetZohoTokenAsync();
        if (zohoToken is null || string.IsNullOrWhiteSpace(zohoToken.AccessToken))
            return NotFound("Zoho access token was not found in the Fretrack database.");

        Console.WriteLine($"Zoho access token from table: {zohoToken.AccessToken}");
        Console.WriteLine($"Zoho api domain from table: {zohoToken.ApiDomain}");

        var apiDomain = NormalizeZohoApiDomain(zohoToken.ApiDomain ?? _configuration["Zoho:ApiDomain"]);
        var accessToken = zohoToken.AccessToken;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var mergedBills = new JsonArray();
        var page = 1;
        var hasMorePage = true;

        while (hasMorePage)
        {
            await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

            var endpoint = BuildZohoListEndpoint(apiDomain, "bills", ZohoOrganizationId, page, perPage);
            var result = await SendZohoRequestAsync(client, endpoint, accessToken, cancellationToken);

            if (result.StatusCode < 200 || result.StatusCode >= 300)
                return StatusCode((int)result.StatusCode, result.Content);

            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;

            if (root.TryGetProperty("bills", out var billsElement) && billsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var bill in billsElement.EnumerateArray())
                {
                    mergedBills.Add(JsonNode.Parse(bill.GetRawText()));
                }
            }

            hasMorePage = root.TryGetProperty("page_context", out var pageContext)
                          && pageContext.TryGetProperty("has_more_page", out var hasMoreElement)
                          && hasMoreElement.ValueKind == JsonValueKind.True;

            page++;
        }

        var response = new JsonObject
        {
            ["code"] = 0,
            ["message"] = "success",
            ["bills"] = mergedBills,
            ["page_context"] = new JsonObject
            {
                ["page"] = 1,
                ["per_page"] = perPage,
                ["has_more_page"] = false,
                ["total_records"] = mergedBills.Count
            }
        };

        return Content(response.ToJsonString(), "application/json");
    }

    [HttpGet("getBillFromZoho")]
    public async Task<IActionResult> GetBillFromZoho([FromQuery] string? billNumber, [FromQuery] string? jobNo = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(billNumber) && string.IsNullOrWhiteSpace(jobNo))
                return BadRequest("Provide billNumber or jobNo.");

            using var client = new HttpClient();

            var zohoToken = await GetZohoTokenAsync();
            if (zohoToken is null || string.IsNullOrWhiteSpace(zohoToken.AccessToken))
                return BadRequest("Zoho access token not found.");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    zohoToken.AccessToken
                );

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            var apiDomain = NormalizeZohoApiDomain(zohoToken.ApiDomain ?? _configuration["Zoho:ApiDomain"]);

            if (!string.IsNullOrWhiteSpace(billNumber))
            {
                var bill = await FetchZohoBillDetailAsync(
                    client,
                    apiDomain,
                    zohoToken.AccessToken,
                    billNumber.Trim(),
                    cancellationToken);

                if (bill is null)
                    return NotFound($"Bill not found: {billNumber.Trim()}");

                await UpsertZohoBillsForGstMappingAsync(bill, cancellationToken);
                return Content(bill.ToJsonString(), "application/json");
            }

            var resolvedCargoIds = await ResolveCargoIdsAsync(null, jobNo, cancellationToken);
            if (resolvedCargoIds.Count == 0)
                return NotFound("No cargo was found for the given jobNo.");

            var billNumbers = new List<string>();
            foreach (var resolvedCargoId in resolvedCargoIds)
            {
                var cargoBillNumbers = await LoadLocalBillNumbersAsync(resolvedCargoId, cancellationToken);
                foreach (var cargoBillNumber in cargoBillNumbers)
                {
                    if (!billNumbers.Contains(cargoBillNumber, StringComparer.OrdinalIgnoreCase))
                        billNumbers.Add(cargoBillNumber);
                }
            }

            if (billNumbers.Count == 0)
                return NotFound("No vendor bill numbers were found for the given jobNo.");

            var bills = new JsonArray();
            foreach (var localBillNumber in billNumbers)
            {
                var bill = await FetchZohoBillDetailAsync(
                    client,
                    apiDomain,
                    zohoToken.AccessToken,
                    localBillNumber,
                    cancellationToken);

                if (bill is null)
                    continue;

                await UpsertZohoBillsForGstMappingAsync(bill, cancellationToken);
                bills.Add(bill);
            }

            var aggregatedResponse = new JsonObject
            {
                ["code"] = 0,
                ["message"] = "success",
                ["job_no"] = jobNo.Trim(),
                ["bill_numbers"] = new JsonArray(billNumbers.Select(n => JsonValue.Create(n)).ToArray()),
                ["bills"] = bills
            };

            return Content(aggregatedResponse.ToJsonString(), "application/json");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return StatusCode(
                500,
                new
                {
                    message = "Error while fetching bill from Zoho.",
                    error = ex.Message
                }
            );
        }
    }

    [HttpGet("getInvoiceFromZoho")]
    public async Task<IActionResult> GetInvoiceFromZoho([FromQuery] string? invoiceNumber, [FromQuery] string? jobNo = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber) && string.IsNullOrWhiteSpace(jobNo))
                return BadRequest("Provide invoiceNumber or jobNo.");

            using var client = new HttpClient();

            var zohoToken = await GetZohoTokenAsync();
            if (zohoToken is null || string.IsNullOrWhiteSpace(zohoToken.AccessToken))
                return BadRequest("Zoho access token not found.");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    zohoToken.AccessToken
                );

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            var apiDomain = NormalizeZohoApiDomain(zohoToken.ApiDomain ?? _configuration["Zoho:ApiDomain"]);

            if (!string.IsNullOrWhiteSpace(invoiceNumber))
            {
                _logger.LogInformation("Fetching Zoho invoice for invoiceNumber {InvoiceNumber}.", invoiceNumber.Trim());
                var invoice = await FetchZohoInvoiceDetailAsync(
                    client,
                    apiDomain,
                    zohoToken.AccessToken,
                    invoiceNumber.Trim(),
                    cancellationToken);

                if (invoice is null)
                    return NotFound($"Invoice not found: {invoiceNumber.Trim()}");

                await UpsertZohoInvoicesForGstMappingAsync(invoice, cancellationToken);
                _logger.LogInformation("Zoho invoice mapping completed for invoiceNumber {InvoiceNumber}.", invoiceNumber.Trim());
                return Content(invoice.ToJsonString(), "application/json");
            }

            var resolvedCargoIds = await ResolveCargoIdsAsync(null, jobNo, cancellationToken);
            if (resolvedCargoIds.Count == 0)
                return NotFound("No cargo was found for the given jobNo.");

            _logger.LogInformation(
                "Resolved {CargoCount} cargo row(s) for Zoho invoice fetch by jobNo {JobNo}.",
                resolvedCargoIds.Count,
                jobNo.Trim());

            var invoiceNumbers = new List<string>();
            var invoiceNumberSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resolvedCargoId in resolvedCargoIds)
            {
                var cargoBillNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cargoInvoiceNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                await LoadLocalDocumentNumbersAsync(resolvedCargoId, cargoBillNumbers, cargoInvoiceNumbers, cancellationToken);

                foreach (var cargoInvoiceNumber in cargoInvoiceNumbers)
                {
                    if (invoiceNumberSet.Add(cargoInvoiceNumber))
                        invoiceNumbers.Add(cargoInvoiceNumber);
                }
            }

            if (invoiceNumbers.Count == 0)
                return NotFound("No invoice numbers were found for the given jobNo.");

            var invoices = new JsonArray();
            foreach (var localInvoiceNumber in invoiceNumbers)
            {
                _logger.LogInformation(
                    "Fetching Zoho invoice for jobNo {JobNo} and invoiceNumber {InvoiceNumber}.",
                    jobNo.Trim(),
                    localInvoiceNumber);

                var invoice = await FetchZohoInvoiceDetailAsync(
                    client,
                    apiDomain,
                    zohoToken.AccessToken,
                    localInvoiceNumber,
                    cancellationToken);

                if (invoice is null)
                {
                    _logger.LogWarning(
                        "Zoho invoice not found for jobNo {JobNo} and invoiceNumber {InvoiceNumber}.",
                        jobNo.Trim(),
                        localInvoiceNumber);
                    continue;
                }

                await UpsertZohoInvoicesForGstMappingAsync(invoice, cancellationToken);
                _logger.LogInformation(
                    "Zoho invoice mapping completed for jobNo {JobNo} and invoiceNumber {InvoiceNumber}.",
                    jobNo.Trim(),
                    localInvoiceNumber);
                invoices.Add(invoice);
            }

            var aggregatedResponse = new JsonObject
            {
                ["code"] = 0,
                ["message"] = "success",
                ["job_no"] = jobNo.Trim(),
                ["invoice_numbers"] = new JsonArray(invoiceNumbers.Select(n => JsonValue.Create(n)).ToArray()),
                ["invoices"] = invoices
            };

            return Content(aggregatedResponse.ToJsonString(), "application/json");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            return StatusCode(
                500,
                new
                {
                    message = "Error while fetching invoice from Zoho.",
                    error = ex.Message
                }
            );
        }
    }

    [HttpGet("zoho/invoices")]
    public async Task<IActionResult> GetInvoicesFromZoho([FromQuery] int perPage = 200, CancellationToken cancellationToken = default)
    {
        perPage = perPage <= 0 ? 200 : Math.Min(perPage, 200);

        var zohoToken = await GetZohoTokenAsync();
        if (zohoToken is null || string.IsNullOrWhiteSpace(zohoToken.AccessToken))
            return NotFound("Zoho access token was not found in the Fretrack database.");

        var apiDomain = NormalizeZohoApiDomain(zohoToken.ApiDomain ?? _configuration["Zoho:ApiDomain"]);
        var accessToken = zohoToken.AccessToken;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var mergedInvoices = new JsonArray();
        var page = 1;
        var hasMorePage = true;

        while (hasMorePage)
        {
            await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

            var endpoint = BuildZohoListEndpoint(apiDomain, "invoices", ZohoOrganizationId, page, perPage);
            var result = await SendZohoRequestAsync(client, endpoint, accessToken, cancellationToken);

            if (result.StatusCode < 200 || result.StatusCode >= 300)
                return StatusCode((int)result.StatusCode, result.Content);

            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;

            if (root.TryGetProperty("invoices", out var invoicesElement) && invoicesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var invoice in invoicesElement.EnumerateArray())
                {
                    mergedInvoices.Add(JsonNode.Parse(invoice.GetRawText()));
                }
            }

            hasMorePage = root.TryGetProperty("page_context", out var pageContext)
                          && pageContext.TryGetProperty("has_more_page", out var hasMoreElement)
                          && hasMoreElement.ValueKind == JsonValueKind.True;

            page++;
        }

        var response = new JsonObject
        {
            ["code"] = 0,
            ["message"] = "success",
            ["invoices"] = mergedInvoices,
            ["page_context"] = new JsonObject
            {
                ["page"] = 1,
                ["per_page"] = perPage,
                ["has_more_page"] = false,
                ["total_records"] = mergedInvoices.Count
            }
        };

        return Content(response.ToJsonString(), "application/json");
    }

    [HttpGet("zoho/by-job")]
    public async Task<IActionResult> GetZohoByJob(
        [FromQuery] string? jobNo,
        [FromQuery] int? cargoId,
        [FromQuery] int perPage = 200,
        CancellationToken cancellationToken = default)
    {
        if (cargoId is null && string.IsNullOrWhiteSpace(jobNo))
            return BadRequest("Either 'jobNo' or 'cargoId' is required.");

        perPage = perPage <= 0 ? 200 : Math.Min(perPage, 200);

        var zohoToken = await GetZohoTokenAsync();
        if (zohoToken is null || string.IsNullOrWhiteSpace(zohoToken.AccessToken))
            return NotFound("Zoho access token was not found in the Fretrack database.");

        var apiDomain = NormalizeZohoApiDomain(zohoToken.ApiDomain ?? _configuration["Zoho:ApiDomain"]);
        var accessToken = zohoToken.AccessToken;

        var resolvedCargoIds = await ResolveCargoIdsAsync(cargoId, jobNo, cancellationToken);
        if (resolvedCargoIds.Count == 0)
            return NotFound("No cargo rows were found for the given jobNo/cargoId.");

        var localBillNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localInvoiceNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resolvedCargoId in resolvedCargoIds)
        {
            await LoadLocalDocumentNumbersAsync(resolvedCargoId, localBillNumbers, localInvoiceNumbers, cancellationToken);
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var bills = await FetchZohoDocumentsByNumbersAsync(
            client,
            apiDomain,
            accessToken,
            ZohoOrganizationId,
            "bills",
            "bill_number",
            "bills",
            localBillNumbers,
            cancellationToken);

        var invoices = await FetchZohoDocumentsByNumbersAsync(
            client,
            apiDomain,
            accessToken,
            ZohoOrganizationId,
            "invoices",
            "invoice_number",
            "invoices",
            localInvoiceNumbers,
            cancellationToken);

        var response = new JsonObject
        {
            ["code"] = 0,
            ["message"] = "success",
            ["job_no"] = jobNo,
            ["cargo_ids"] = new JsonArray(resolvedCargoIds.Select(id => JsonValue.Create(id)).ToArray()),
            ["bill_numbers"] = new JsonArray(localBillNumbers.Select(n => JsonValue.Create(n)).ToArray()),
            ["invoice_numbers"] = new JsonArray(localInvoiceNumbers.Select(n => JsonValue.Create(n)).ToArray()),
            ["bills"] = bills,
            ["invoices"] = invoices
        };

        return Content(response.ToJsonString(), "application/json");
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

    private static string NormalizeZohoApiDomain(string? apiDomain)
    {
        if (string.IsNullOrWhiteSpace(apiDomain))
            return "https://books.zoho.in";

        var value = apiDomain.Trim().TrimEnd('/');
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value.TrimStart('/');
        }

        return value.TrimEnd('/');
    }

    private static string BuildZohoListEndpoint(string apiDomain, string resourceName, long organizationId, int page, int perPage)
    {
        return $"{apiDomain.TrimEnd('/')}/books/v3/{resourceName}?organization_id={organizationId}&page={page}&per_page={perPage}";
    }

    private async Task WaitForZohoRequestSlotAsync(long organizationId, CancellationToken cancellationToken)
    {
        var limiter = ZohoRequestLimiters.GetOrAdd(organizationId, _ => new SlidingWindowRequestLimiter(ZohoRequestsPerMinuteLimit, TimeSpan.FromMinutes(1)));
        var delay = limiter.GetDelay();

        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Zoho request throttled for organization {OrganizationId}. Waiting {DelayMilliseconds} ms before the next call.",
                organizationId,
                (int)delay.TotalMilliseconds);

            await Task.Delay(delay, cancellationToken);
        }

        limiter.MarkRequest();
    }

    private static async Task<(int StatusCode, string Content)> SendZohoRequestAsync(HttpClient client, string endpoint, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, content);
    }

    private async Task<IReadOnlyList<int>> ResolveCargoIdsAsync(int? cargoId, string? jobNo, CancellationToken cancellationToken)
    {
        var cargoIds = new List<int>();

        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (cargoId.HasValue)
        {
            await using var command = new SqlCommand(
                @"SELECT CargoID
                  FROM Cargo
                  WHERE CargoID = @CargoID", connection);
            command.Parameters.AddWithValue("@CargoID", cargoId.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                cargoIds.Add(reader.GetInt32(0));
            }

            return cargoIds;
        }

        if (string.IsNullOrWhiteSpace(jobNo))
            return cargoIds;

        await using var jobCommand = new SqlCommand(
            @"SELECT CargoID
              FROM Cargo
              WHERE LTRIM(RTRIM(JobNo)) = LTRIM(RTRIM(@JobNo))", connection);
        jobCommand.Parameters.AddWithValue("@JobNo", jobNo.Trim());

        await using var jobReader = await jobCommand.ExecuteReaderAsync(cancellationToken);
        while (await jobReader.ReadAsync(cancellationToken))
        {
            cargoIds.Add(jobReader.GetInt32(0));
        }

        return cargoIds;
    }

    private async Task LoadLocalDocumentNumbersAsync(
        int cargoId,
        ISet<string> billNumbers,
        ISet<string> invoiceNumbers,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var billCommand = new SqlCommand(
            @"SELECT VendorBillNumber
              FROM VendorBill
              WHERE CargoID = @CargoID
                AND ISNULL(isDeleted, 0) = 0
                AND VendorBillNumber IS NOT NULL
                AND LTRIM(RTRIM(VendorBillNumber)) <> ''", connection))
        {
            billCommand.Parameters.AddWithValue("@CargoID", cargoId);

            await using var billReader = await billCommand.ExecuteReaderAsync(cancellationToken);
            while (await billReader.ReadAsync(cancellationToken))
            {
                var number = billReader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(number))
                    billNumbers.Add(number);
            }
        }

        await using (var invoiceCommand = new SqlCommand(
            @"SELECT InvoiceNumber
              FROM Invoices
              WHERE CargoID = @CargoID
                AND ISNULL(isDeleted, 0) = 0
                AND InvoiceNumber IS NOT NULL
                AND LTRIM(RTRIM(InvoiceNumber)) <> ''", connection))
        {
            invoiceCommand.Parameters.AddWithValue("@CargoID", cargoId);

            await using var invoiceReader = await invoiceCommand.ExecuteReaderAsync(cancellationToken);
            while (await invoiceReader.ReadAsync(cancellationToken))
            {
                var number = invoiceReader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(number))
                    invoiceNumbers.Add(number);
            }
        }
    }

    private async Task<IReadOnlyList<string>> LoadLocalBillNumbersAsync(int cargoId, CancellationToken cancellationToken)
    {
        var billNumbers = new List<string>();

        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var billCommand = new SqlCommand(
            @"SELECT VendorBillNumber
              FROM VendorBill
              WHERE CargoID = @CargoID
                AND ISNULL(isDeleted, 0) = 0
                AND VendorBillNumber IS NOT NULL
                AND LTRIM(RTRIM(VendorBillNumber)) <> ''", connection);
        billCommand.Parameters.AddWithValue("@CargoID", cargoId);

        await using var billReader = await billCommand.ExecuteReaderAsync(cancellationToken);
        while (await billReader.ReadAsync(cancellationToken))
        {
            var number = billReader.GetString(0).Trim();
            if (!string.IsNullOrWhiteSpace(number))
                billNumbers.Add(number);
        }

        return billNumbers;
    }

    private async Task<JsonArray> FetchZohoBillsByNumbersAsync(
        HttpClient client,
        string apiDomain,
        string accessToken,
        long organizationId,
        IEnumerable<string> billNumbers,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();
        var seenBillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var billNumber in billNumbers.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await WaitForZohoRequestSlotAsync(organizationId, cancellationToken);

            var candidates = new[]
            {
                ("bill_number", billNumber),
                ("bill_number_contains", billNumber),
                ("reference_number", billNumber),
                ("reference_number_contains", billNumber),
                ("search_text", billNumber)
            };

            foreach (var (queryName, queryValue) in candidates)
            {
                var endpoint = $"{apiDomain.TrimEnd('/')}/books/v3/bills?organization_id={organizationId}&{queryName}={Uri.EscapeDataString(queryValue)}&page=1&per_page=200";
                var result = await SendZohoRequestAsync(client, endpoint, accessToken, cancellationToken);

                if (result.StatusCode < 200 || result.StatusCode >= 300)
                    break;

                using var document = JsonDocument.Parse(result.Content);
                var root = document.RootElement;

                if (!root.TryGetProperty("bills", out var billsElement) || billsElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in billsElement.EnumerateArray())
                {
                    if (item.TryGetProperty("bill_id", out var billIdElement))
                    {
                        var billId = billIdElement.ToString();
                        if (!string.IsNullOrWhiteSpace(billId) && !seenBillIds.Add(billId))
                            continue;
                    }

                    var matched = false;
                    if (item.TryGetProperty("bill_number", out var numberElement))
                    {
                        var zohoBillNumber = numberElement.GetString()?.Trim();
                        matched = string.Equals(zohoBillNumber, billNumber.Trim(), StringComparison.OrdinalIgnoreCase)
                                  || NormalizeBillText(zohoBillNumber).Contains(NormalizeBillText(billNumber), StringComparison.OrdinalIgnoreCase);
                    }

                    if (!matched && item.TryGetProperty("reference_number", out var referenceElement))
                    {
                        var referenceNumber = referenceElement.GetString()?.Trim();
                        matched = string.Equals(referenceNumber, billNumber.Trim(), StringComparison.OrdinalIgnoreCase)
                                  || NormalizeBillText(referenceNumber).Contains(NormalizeBillText(billNumber), StringComparison.OrdinalIgnoreCase);
                    }

                    if (matched || queryName == "search_text")
                    {
                        results.Add(JsonNode.Parse(item.GetRawText()));
                    }
                }

                if (results.Count > 0)
                    break;
            }
        }

        return results;
    }

    private async Task<JsonArray> FetchZohoBillsByCatalogAsync(
        HttpClient client,
        string apiDomain,
        string accessToken,
        long organizationId,
        IEnumerable<string> billNumbers,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();
        var seenBillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedTargets = billNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => (Original: n.Trim(), Normalized: NormalizeBillText(n)))
            .ToList();

        var page = 1;
        var hasMorePage = true;

        while (hasMorePage)
        {
            await WaitForZohoRequestSlotAsync(organizationId, cancellationToken);

            var endpoint = BuildZohoListEndpoint(apiDomain, "bills", organizationId, page, 200);
            var result = await SendZohoRequestAsync(client, endpoint, accessToken, cancellationToken);
            if (result.StatusCode < 200 || result.StatusCode >= 300)
                break;

            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;

            if (root.TryGetProperty("bills", out var billsElement) && billsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in billsElement.EnumerateArray())
                {
                    var billId = item.TryGetProperty("bill_id", out var billIdElement) ? billIdElement.ToString() : null;
                    if (!string.IsNullOrWhiteSpace(billId) && !seenBillIds.Add(billId))
                        continue;

                    var zohoBillNumber = item.TryGetProperty("bill_number", out var numberElement)
                        ? numberElement.GetString()
                        : null;
                    var zohoReferenceNumber = item.TryGetProperty("reference_number", out var referenceElement)
                        ? referenceElement.GetString()
                        : null;

                    var matched = normalizedTargets.Any(target =>
                        IsBillMatch(zohoBillNumber, zohoReferenceNumber, target.Original, target.Normalized));

                    if (matched)
                        results.Add(JsonNode.Parse(item.GetRawText()));
                }
            }

            hasMorePage = root.TryGetProperty("page_context", out var pageContext)
                          && pageContext.TryGetProperty("has_more_page", out var hasMoreElement)
                          && hasMoreElement.ValueKind == JsonValueKind.True;

            page++;
        }

        return results;
    }

    private static bool IsBillMatch(string? zohoBillNumber, string? zohoReferenceNumber, string original, string normalizedTarget)
    {
        var normalizedBillNumber = NormalizeBillText(zohoBillNumber);
        var normalizedReferenceNumber = NormalizeBillText(zohoReferenceNumber);

        return string.Equals(zohoBillNumber?.Trim(), original, StringComparison.OrdinalIgnoreCase)
               || string.Equals(zohoReferenceNumber?.Trim(), original, StringComparison.OrdinalIgnoreCase)
               || normalizedBillNumber.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase)
               || normalizedReferenceNumber.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInvoiceMatch(string? zohoInvoiceNumber, string? zohoReferenceNumber, string original)
    {
        var normalizedOriginal = NormalizeBillText(original);
        var normalizedInvoiceNumber = NormalizeBillText(zohoInvoiceNumber);
        var normalizedReferenceNumber = NormalizeBillText(zohoReferenceNumber);

        return string.Equals(zohoInvoiceNumber?.Trim(), original, StringComparison.OrdinalIgnoreCase)
               || string.Equals(zohoReferenceNumber?.Trim(), original, StringComparison.OrdinalIgnoreCase)
               || normalizedInvoiceNumber.Contains(normalizedOriginal, StringComparison.OrdinalIgnoreCase)
               || normalizedReferenceNumber.Contains(normalizedOriginal, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBillText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim();
        normalized = normalized.Replace(" ", string.Empty);
        normalized = normalized.Replace("_", string.Empty);
        normalized = normalized.Replace("-", string.Empty);
        normalized = normalized.Replace("/", string.Empty);
        return normalized.ToUpperInvariant();
    }

    private async Task<JsonArray> FetchZohoDocumentsByNumbersAsync(
        HttpClient client,
        string apiDomain,
        string accessToken,
        long organizationId,
        string resourceName,
        string numberQueryParamName,
        string responseArrayName,
        IEnumerable<string> numbers,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();

        foreach (var number in numbers.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await WaitForZohoRequestSlotAsync(organizationId, cancellationToken);

            var endpoint = $"{apiDomain.TrimEnd('/')}/books/v3/{resourceName}?organization_id={organizationId}&{numberQueryParamName}={Uri.EscapeDataString(number)}&page=1&per_page=200";
            var result = await SendZohoRequestAsync(client, endpoint, accessToken, cancellationToken);

            if (result.StatusCode < 200 || result.StatusCode >= 300)
                return results;

            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;

            if (!root.TryGetProperty(responseArrayName, out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty(numberQueryParamName, out var itemNumberElement))
                {
                    var itemNumber = itemNumberElement.GetString();
                    if (!string.Equals(itemNumber?.Trim(), number.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                results.Add(JsonNode.Parse(item.GetRawText()));
            }
        }

        return results;
    }

    private async Task<JsonObject?> FetchZohoBillDetailAsync(
        HttpClient client,
        string apiDomain,
        string accessToken,
        string billNumber,
        CancellationToken cancellationToken)
    {
        await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

        var searchUrl =
            $"{apiDomain.TrimEnd('/')}/books/v3/bills"
            + $"?organization_id={ZohoOrganizationId}"
            + "&bill_number=" + Uri.EscapeDataString(billNumber);

        Console.WriteLine("Zoho URL: " + searchUrl);

        using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

        using var searchResponse = await client.SendAsync(searchRequest, cancellationToken);
        var searchContent = await searchResponse.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine("Zoho Response:");
        Console.WriteLine(searchContent);

        if (!searchResponse.IsSuccessStatusCode)
            throw new HttpRequestException(searchContent);

        string? billId = null;
        using (var searchJson = JsonDocument.Parse(searchContent))
        {
            var root = searchJson.RootElement;
            if (!root.TryGetProperty("bills", out var bills) ||
                bills.ValueKind != JsonValueKind.Array ||
                bills.GetArrayLength() == 0)
            {
                return null;
            }

            billId = GetJsonString(bills[0], "bill_id");
        }

        if (string.IsNullOrWhiteSpace(billId))
            return null;

        await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

        var detailUrl =
            $"{apiDomain.TrimEnd('/')}/books/v3/bills/"
            + Uri.EscapeDataString(billId)
            + $"?organization_id={ZohoOrganizationId}";

        Console.WriteLine("Zoho Detail URL: " + detailUrl);

        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, detailUrl);
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

        using var detailResponse = await client.SendAsync(detailRequest, cancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine("Zoho Detail Response:");
        Console.WriteLine(detailContent);

        if (!detailResponse.IsSuccessStatusCode)
            throw new HttpRequestException(detailContent);

        using var detailJson = JsonDocument.Parse(detailContent);
        var detailRoot = detailJson.RootElement;
        if (!detailRoot.TryGetProperty("bill", out var bill))
            return null;

        return BuildZohoBillPayload(bill);
    }

    private static JsonObject BuildZohoBillPayload(JsonElement bill)
    {
        var vendorName = GetJsonString(bill, "vendor_name", "contact_name", "customer_name", "vendor_name_label");
        var status = GetJsonString(bill, "status");
        var zohoBillNumber = GetJsonString(bill, "bill_number");
        var billDate = GetJsonDateText(bill, "bill_date", "date");
        var dueDate = GetJsonDateText(bill, "due_date");
        var currencyCode = GetJsonString(bill, "currency_code");
        var gstNo = GetJsonString(bill, "gst_no");
        var gstTreatment = GetJsonString(bill, "gst_treatment");
        var sourceOfSupply = GetJsonString(bill, "source_of_supply");
        var destinationOfSupply = GetJsonString(bill, "destination_of_supply", "place_of_supply");
        var paymentTerms = GetJsonString(bill, "payment_terms", "payment_terms_label");
        var exchangeRate = GetJsonDecimal(bill, "exchange_rate") ?? 1m;
        var subTotal = GetJsonDecimal(bill, "sub_total") ?? 0m;

        var isRegistered = !string.IsNullOrWhiteSpace(gstNo);
        var isIntraState =
            !string.IsNullOrWhiteSpace(sourceOfSupply) &&
            !string.IsNullOrWhiteSpace(destinationOfSupply) &&
            sourceOfSupply.Equals(destinationOfSupply, StringComparison.OrdinalIgnoreCase);

        var vendorBillGSTTreatment =
            isRegistered && isIntraState ? "Intra State Registered" :
            isRegistered ? "Inter State Registered" :
            isIntraState ? "Intra State UnRegistered" :
            "Inter State UnRegistered";

        return new JsonObject
        {
            ["vendor_name"] = vendorName,
            ["status"] = status,
            ["bill_number"] = zohoBillNumber,
            ["bill_date"] = billDate,
            ["due_date"] = dueDate,
            ["currency_code"] = currencyCode,
            ["exchange_rate"] = exchangeRate,
            ["bcy_sub_total"] = Math.Round(subTotal * exchangeRate, 2),
            ["fcy_sub_total"] = subTotal,
            ["gst_no"] = gstNo,
            ["gst_treatment"] = gstTreatment,
            ["source_of_supply"] = sourceOfSupply,
            ["destination_of_supply"] = destinationOfSupply,
            ["payment_terms"] = paymentTerms,
            ["vendorBillGSTTreatment"] = vendorBillGSTTreatment
        };
    }

    private async Task<JsonObject?> FetchZohoInvoiceDetailAsync(
        HttpClient client,
        string apiDomain,
        string accessToken,
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            ("invoice_number", invoiceNumber),
            ("invoice_number_contains", invoiceNumber),
            ("reference_number", invoiceNumber),
            ("reference_number_contains", invoiceNumber),
            ("search_text", invoiceNumber)
        };

        foreach (var (queryName, queryValue) in candidates)
        {
            await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

            var searchUrl =
                $"{apiDomain.TrimEnd('/')}/books/v3/invoices"
                + $"?organization_id={ZohoOrganizationId}"
                + $"&{queryName}=" + Uri.EscapeDataString(queryValue);

            _logger.LogInformation("Zoho invoice search URL for {InvoiceNumber}: {SearchUrl}", invoiceNumber, searchUrl);

            using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

            using var searchResponse = await client.SendAsync(searchRequest, cancellationToken);
            var searchContent = await searchResponse.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Zoho invoice search response for {InvoiceNumber}: {Response}", invoiceNumber, searchContent);

            if (!searchResponse.IsSuccessStatusCode)
                throw new HttpRequestException(searchContent);

            string? invoiceId = null;
            using (var searchJson = JsonDocument.Parse(searchContent))
            {
                var root = searchJson.RootElement;
                if (!root.TryGetProperty("invoices", out var invoices) ||
                    invoices.ValueKind != JsonValueKind.Array ||
                    invoices.GetArrayLength() == 0)
                {
                    continue;
                }

                foreach (var candidate in invoices.EnumerateArray())
                {
                    var zohoInvoiceNumber = GetJsonString(candidate, "invoice_number");
                    var zohoReferenceNumber = GetJsonString(candidate, "reference_number");

                    if (!IsInvoiceMatch(zohoInvoiceNumber, zohoReferenceNumber, invoiceNumber))
                        continue;

                    invoiceId = GetJsonString(candidate, "invoice_id");
                    if (!string.IsNullOrWhiteSpace(invoiceId))
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(invoiceId))
                continue;

            await WaitForZohoRequestSlotAsync(ZohoOrganizationId, cancellationToken);

            var detailUrl =
                $"{apiDomain.TrimEnd('/')}/books/v3/invoices/"
                + Uri.EscapeDataString(invoiceId)
                + $"?organization_id={ZohoOrganizationId}";

            _logger.LogInformation("Zoho invoice detail URL for {InvoiceNumber}: {DetailUrl}", invoiceNumber, detailUrl);

            using var detailRequest = new HttpRequestMessage(HttpMethod.Get, detailUrl);
            detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);

            using var detailResponse = await client.SendAsync(detailRequest, cancellationToken);
            var detailContent = await detailResponse.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Zoho invoice detail response for {InvoiceNumber}: {Response}", invoiceNumber, detailContent);

            if (!detailResponse.IsSuccessStatusCode)
                throw new HttpRequestException(detailContent);

            using var detailJson = JsonDocument.Parse(detailContent);
            var detailRoot = detailJson.RootElement;
            if (!detailRoot.TryGetProperty("invoice", out var invoice))
                continue;

            return BuildZohoInvoicePayload(invoice);
        }

        return null;
    }

    private static JsonObject BuildZohoInvoicePayload(JsonElement invoice)
    {
        var customerName = GetJsonString(invoice, "customer_name");
        var status = GetJsonString(invoice, "status");
        var zohoInvoiceNumber = GetJsonString(invoice, "invoice_number");
        var invoiceDate = GetJsonDateText(invoice, "invoice_date", "date");
        var dueDate = GetJsonDateText(invoice, "due_date");
        var currencyCode = GetJsonString(invoice, "currency_code");
        var gstNo = GetJsonString(invoice, "gst_no");
        var gstTreatment = GetJsonString(invoice, "gst_treatment");
        var sourceOfSupply = GetJsonString(invoice, "source_of_supply");
        var destinationOfSupply = GetJsonString(invoice, "destination_of_supply", "place_of_supply");
        var paymentTerms = GetJsonString(invoice, "payment_terms", "payment_terms_label");
        var exchangeRate = GetJsonDecimal(invoice, "exchange_rate") ?? 1m;
        var subTotal = GetJsonDecimal(invoice, "sub_total") ?? 0m;

        var isRegistered = !string.IsNullOrWhiteSpace(gstNo);
        var isIntraState =
            !string.IsNullOrWhiteSpace(sourceOfSupply) &&
            !string.IsNullOrWhiteSpace(destinationOfSupply) &&
            sourceOfSupply.Equals(destinationOfSupply, StringComparison.OrdinalIgnoreCase);

        var customerInvoiceGSTTreatment =
            isRegistered && isIntraState ? "Intra State Registered" :
            isRegistered ? "Inter State Registered" :
            isIntraState ? "Intra State UnRegistered" :
            "Inter State UnRegistered";

        return new JsonObject
        {
            ["customer_name"] = customerName,
            ["status"] = status,
            ["invoice_number"] = zohoInvoiceNumber,
            ["date"] = invoiceDate,
            ["invoice_date"] = invoiceDate,
            ["due_date"] = dueDate,
            ["currency_code"] = currencyCode,
            ["exchange_rate"] = exchangeRate,
            ["bcy_sub_total"] = Math.Round(subTotal * exchangeRate, 2),
            ["fcy_sub_total"] = subTotal,
            ["gst_no"] = gstNo,
            ["gst_treatment"] = gstTreatment,
            ["source_of_supply"] = sourceOfSupply ?? string.Empty,
            ["place_of_supply"] = destinationOfSupply ?? string.Empty,
            ["destination_of_supply"] = destinationOfSupply ?? string.Empty,
            ["payment_terms"] = paymentTerms,
            ["customerInvoiceGSTTreatment"] = customerInvoiceGSTTreatment
        };
    }

    private async Task UpsertZohoBillsForGstMappingAsync(JsonObject bill, CancellationToken cancellationToken)
    {
        var billNumber = bill["bill_number"]?.ToString();
        if (string.IsNullOrWhiteSpace(billNumber))
            return;

        var connectionString = _configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableSchema = await ResolveTableSchemaAsync(connection, "Fretrack_ZohoBillsForGSTMapping", cancellationToken);

        using var billDocument = JsonDocument.Parse(bill.ToJsonString());
        await UpsertZohoBillMappingRowAsync(connection, tableSchema, billDocument.RootElement, cancellationToken);
    }

    private async Task UpsertZohoBillsForGstMappingAsync(string responseContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return;

        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (!root.TryGetProperty("bills", out var billsElement) || billsElement.ValueKind != JsonValueKind.Array)
            return;

        var connectionString = _configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableSchema = await ResolveTableSchemaAsync(connection, "Fretrack_ZohoBillsForGSTMapping", cancellationToken);

        foreach (var bill in billsElement.EnumerateArray())
        {
            await UpsertZohoBillMappingRowAsync(connection, tableSchema, bill, cancellationToken);
        }
    }

    private async Task UpsertZohoInvoicesForGstMappingAsync(string responseContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return;

        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (!root.TryGetProperty("invoices", out var invoicesElement) || invoicesElement.ValueKind != JsonValueKind.Array)
            return;

        var connectionString = _configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableSchema = await ResolveTableSchemaAsync(connection, "Fretrack_zohoInvoicesForGSTMapping", cancellationToken);

        foreach (var invoice in invoicesElement.EnumerateArray())
        {
            await UpsertZohoInvoiceMappingRowAsync(connection, tableSchema, invoice, cancellationToken);
        }
    }

    private async Task UpsertZohoInvoicesForGstMappingAsync(JsonObject invoice, CancellationToken cancellationToken)
    {
        var invoiceNumber = invoice["invoice_number"]?.ToString();
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return;

        var connectionString = _configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var tableSchema = await ResolveTableSchemaAsync(connection, "Fretrack_zohoInvoicesForGSTMapping", cancellationToken);

        var existing = await InvoiceMappingExistsAsync(connection, tableSchema, invoiceNumber.Trim(), cancellationToken);
        _logger.LogInformation(
            "Upserting Zoho invoice mapping for invoiceNumber {InvoiceNumber} into schema {Schema}. Action={Action}",
            invoiceNumber.Trim(),
            tableSchema,
            existing ? "update" : "insert");

        using var invoiceDocument = JsonDocument.Parse(invoice.ToJsonString());
        await UpsertZohoInvoiceMappingRowAsync(connection, tableSchema, invoiceDocument.RootElement, cancellationToken);
    }

    private async Task UpsertZohoBillMappingRowAsync(SqlConnection connection, string tableSchema, JsonElement bill, CancellationToken cancellationToken)
    {
        var billNumber = GetJsonString(bill, "bill_number");
        if (string.IsNullOrWhiteSpace(billNumber))
            return;

        var tableName = BracketQualifiedName(tableSchema, "Fretrack_ZohoBillsForGSTMapping");
        await using var command = new SqlCommand($@"
IF EXISTS
(
    SELECT 1
    FROM {tableName}
    WHERE LTRIM(RTRIM(bill_number)) = LTRIM(RTRIM(@bill_number))
)
BEGIN
    UPDATE {tableName}
    SET vendor_name = @vendor_name,
        status = @status,
        bill_date = @bill_date,
        due_date = @due_date,
        currency_code = @currency_code,
        exchange_rate = @exchange_rate,
        bcy_sub_total = @bcy_sub_total,
        fcy_sub_total = @fcy_sub_total,
        gst_no = @gst_no,
        gst_treatment = @gst_treatment,
        source_of_supply = @source_of_supply,
        destination_of_supply = @destination_of_supply,
        payment_terms = @payment_terms,
        VendorBillGSTTreatment = @VendorBillGSTTreatment,
        paymentTermID = @paymentTermID,
        IsSelectedForMigration = 1,
        MigrationRemarks = 'ZohoBill',
        ImportedOn = GETDATE()
    WHERE LTRIM(RTRIM(bill_number)) = LTRIM(RTRIM(@bill_number));
END
ELSE
BEGIN
    INSERT INTO {tableName}
    (
        vendor_name,
        status,
        bill_number,
        bill_date,
        due_date,
        currency_code,
        exchange_rate,
        bcy_sub_total,
        fcy_sub_total,
        gst_no,
        gst_treatment,
        source_of_supply,
        destination_of_supply,
        payment_terms,
        VendorBillGSTTreatment,
        paymentTermID,
        IsSelectedForMigration,
        MigrationRemarks,
        ImportedOn
    )
    VALUES
    (
        @vendor_name,
        @status,
        @bill_number,
        @bill_date,
        @due_date,
        @currency_code,
        @exchange_rate,
        @bcy_sub_total,
        @fcy_sub_total,
        @gst_no,
        @gst_treatment,
        @source_of_supply,
        @destination_of_supply,
        @payment_terms,
        @VendorBillGSTTreatment,
        @paymentTermID,
        1,
        'ZohoBill',
        GETDATE()
    );
END", connection);

        AddSqlParameter(command, "@vendor_name", GetJsonString(bill, "vendor_name", "contact_name", "customer_name", "vendor_name_label"));
        AddSqlParameter(command, "@status", GetJsonString(bill, "status"));
        AddSqlParameter(command, "@bill_number", billNumber.Trim());
        AddSqlParameter(command, "@bill_date", GetJsonDateValue(bill, "bill_date", "date"));
        AddSqlParameter(command, "@due_date", GetJsonDateValue(bill, "due_date"));
        AddSqlParameter(command, "@currency_code", GetJsonString(bill, "currency_code"));
        AddSqlParameter(command, "@exchange_rate", GetJsonDecimal(bill, "exchange_rate"));
        AddSqlParameter(command, "@bcy_sub_total", GetJsonDecimal(bill, "bcy_sub_total", "sub_total"));
        AddSqlParameter(command, "@fcy_sub_total", GetJsonDecimal(bill, "fcy_sub_total", "total", "total_amount"));
        AddSqlParameter(command, "@gst_no", GetJsonString(bill, "gst_no"));
        AddSqlParameter(command, "@gst_treatment", GetJsonString(bill, "gst_treatment"));
        AddSqlParameter(command, "@source_of_supply", GetJsonString(bill, "source_of_supply"));
        AddSqlParameter(command, "@destination_of_supply", GetJsonString(bill, "destination_of_supply", "place_of_supply"));
        AddSqlParameter(command, "@payment_terms", GetJsonString(bill, "payment_terms", "payment_terms_label"));
        AddSqlParameter(command, "@VendorBillGSTTreatment", GetJsonString(bill, "vendorBillGSTTreatment", "VendorBillGSTTreatment", "bill_gsttype", "gst_treatment"));
        AddSqlParameter(command, "@paymentTermID", GetJsonInt(bill, "paymentTermID", "payment_term_id"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpsertZohoInvoiceMappingRowAsync(SqlConnection connection, string tableSchema, JsonElement invoice, CancellationToken cancellationToken)
    {
        var invoiceNumber = GetJsonString(invoice, "invoice_number");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return;

        var tableName = BracketQualifiedName(tableSchema, "Fretrack_zohoInvoicesForGSTMapping");
        await using var command = new SqlCommand($@"
IF EXISTS
(
    SELECT 1
    FROM {tableName}
    WHERE LTRIM(RTRIM(invoice_number)) = LTRIM(RTRIM(@invoice_number))
)
BEGIN
    UPDATE {tableName}
    SET status = @status,
        [date] = @date,
        due_date = @due_date,
        customer_name = @customer_name,
        place_of_supply = @place_of_supply,
        gst_no = @gst_no,
        gst_treatment = @gst_treatment,
        currency_code = @currency_code,
        exchange_rate = @exchange_rate,
        payment_terms = @payment_terms,
        CreatedDate = GETDATE()
    WHERE LTRIM(RTRIM(invoice_number)) = LTRIM(RTRIM(@invoice_number));
END
ELSE
BEGIN
    INSERT INTO {tableName}
    (
        status,
        [date],
        due_date,
        invoice_number,
        customer_name,
        place_of_supply,
        gst_no,
        gst_treatment,
        currency_code,
        exchange_rate,
        payment_terms,
        CreatedDate
    )
    VALUES
    (
        @status,
        @date,
        @due_date,
        @invoice_number,
        @customer_name,
        @place_of_supply,
        @gst_no,
        @gst_treatment,
        @currency_code,
        @exchange_rate,
        @payment_terms,
        GETDATE()
    );
END", connection);

        AddSqlParameter(command, "@status", GetJsonString(invoice, "status"));
        AddSqlParameter(command, "@date", GetJsonDateValue(invoice, "date", "invoice_date"));
        AddSqlParameter(command, "@due_date", GetJsonDateValue(invoice, "due_date"));
        AddSqlParameter(command, "@invoice_number", invoiceNumber.Trim());
        AddSqlParameter(command, "@customer_name", GetJsonString(invoice, "customer_name"));
        AddSqlParameter(command, "@place_of_supply", GetJsonString(invoice, "place_of_supply", "destination_of_supply"));
        AddSqlParameter(command, "@gst_no", GetJsonString(invoice, "gst_no"));
        AddSqlParameter(command, "@gst_treatment", GetJsonString(invoice, "gst_treatment"));
        AddSqlParameter(command, "@currency_code", GetJsonString(invoice, "currency_code"));
        AddSqlParameter(command, "@exchange_rate", GetJsonDecimal(invoice, "exchange_rate"));
        AddSqlParameter(command, "@payment_terms", GetJsonString(invoice, "payment_terms", "payment_terms_label"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> InvoiceMappingExistsAsync(SqlConnection connection, string tableSchema, string invoiceNumber, CancellationToken cancellationToken)
    {
        var tableName = BracketQualifiedName(tableSchema, "Fretrack_zohoInvoicesForGSTMapping");
        await using var command = new SqlCommand($@"
SELECT COUNT(1)
FROM {tableName}
WHERE LTRIM(RTRIM(invoice_number)) = LTRIM(RTRIM(@invoice_number));", connection);

        command.Parameters.AddWithValue("@invoice_number", invoiceNumber);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static void AddSqlParameter(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static string? GetJsonString(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetJsonProperty(element, out var property, propertyNames))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => property.GetRawText()
        };
    }

    private static decimal? GetJsonDecimal(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetJsonProperty(element, out var property, propertyNames))
            return null;

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetDecimal(out var number))
                return number;

            return null;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static int? GetJsonInt(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetJsonProperty(element, out var property, propertyNames))
            return null;

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt32(out var number))
                return number;

            if (property.TryGetInt64(out var longNumber))
                return (int)longNumber;

            return null;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static object? GetJsonDateValue(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetJsonProperty(element, out var property, propertyNames))
            return null;

        if (property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;

            return text;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDateTime(out var parsedDate))
            return parsedDate;

        var raw = property.GetRawText().Trim('"');
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static string? GetJsonDateText(JsonElement element, params string[] propertyNames)
    {
        var value = GetJsonDateValue(element, propertyNames);
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var parsed) =>
                parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            string text => text,
            _ => value.ToString()
        };
    }

    private static bool TryGetJsonProperty(JsonElement element, out JsonElement property, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out property))
                return true;
        }

        foreach (var jsonProperty in element.EnumerateObject())
        {
            foreach (var propertyName in propertyNames)
            {
                if (string.Equals(jsonProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = jsonProperty.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string BracketQualifiedName(string schema, string table)
    {
        var safeSchema = schema.Replace("]", "]]", StringComparison.Ordinal);
        var safeTable = table.Replace("]", "]]", StringComparison.Ordinal);
        return $"[{safeSchema}].[{safeTable}]";
    }

    private static async Task<string> ResolveTableSchemaAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(@"
SELECT TOP 1 TABLE_SCHEMA
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = @TableName
ORDER BY CASE WHEN TABLE_SCHEMA = 'dbo' THEN 0 ELSE 1 END, TABLE_SCHEMA;", connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        var schema = await command.ExecuteScalarAsync(cancellationToken);
        if (schema is null || schema == DBNull.Value)
            throw new KeyNotFoundException($"Table '{tableName}' was not found in CargoMintDB.");

        return Convert.ToString(schema) ?? throw new KeyNotFoundException($"Table '{tableName}' was not found in CargoMintDB.");
    }

    private async Task<ZohoTokenRecord?> GetZohoTokenAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"SELECT TOP 1 *
              FROM zohoTokens
              WHERE Id = 1", connection);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new ZohoTokenRecord(
            GetInt(reader, "Id", "ID") ?? 1,
            GetString(reader, "access_token", "AccessToken"),
            GetString(reader, "api_domain", "ApiDomain"),
            GetString(reader, "refresh_token", "RefreshToken"),
            GetString(reader, "token_type", "TokenType"),
            GetInt(reader, "expires_in", "ExpiresIn"),
            GetDateTime(reader, "expired_date", "ExpiredDate"));
    }

    private static string? GetString(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            var ordinal = GetOrdinalIfExists(reader, name);
            if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                return reader.GetValue(ordinal)?.ToString();
        }

        return null;
    }

    private static int? GetInt(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            var ordinal = GetOrdinalIfExists(reader, name);
            if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                return Convert.ToInt32(reader.GetValue(ordinal));
        }

        return null;
    }

    private static DateTime? GetDateTime(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            var ordinal = GetOrdinalIfExists(reader, name);
            if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                return Convert.ToDateTime(reader.GetValue(ordinal));
        }

        return null;
    }

    private static int GetOrdinalIfExists(SqlDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private sealed class SlidingWindowRequestLimiter
    {
        private readonly int _limit;
        private readonly TimeSpan _window;
        private readonly Queue<DateTimeOffset> _requests = new();
        private readonly object _sync = new();

        public SlidingWindowRequestLimiter(int limit, TimeSpan window)
        {
            _limit = limit;
            _window = window;
        }

        public TimeSpan GetDelay()
        {
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                while (_requests.Count > 0 && now - _requests.Peek() >= _window)
                {
                    _requests.Dequeue();
                }

                if (_requests.Count < _limit)
                    return TimeSpan.Zero;

                var oldest = _requests.Peek();
                var delay = oldest + _window - now;
                return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
            }
        }

        public void MarkRequest()
        {
            lock (_sync)
            {
                var now = DateTimeOffset.UtcNow;
                while (_requests.Count > 0 && now - _requests.Peek() >= _window)
                {
                    _requests.Dequeue();
                }

                _requests.Enqueue(now);
            }
        }
    }

    private sealed record ZohoTokenRecord(
        int Id,
        string? AccessToken,
        string? ApiDomain,
        string? RefreshToken,
        string? TokenType,
        int? ExpiresIn,
        DateTime? ExpiredDate);
}
