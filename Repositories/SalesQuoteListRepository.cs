using GeneratedModels;
using Microsoft.Data.SqlClient;
using System.Linq;
using RepoDb;

namespace RepoDbApi.Repositories;

public class SalesQuoteListRepository : ISalesQuoteListRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SalesQuoteListRepository> _logger;

    public SalesQuoteListRepository(IConfiguration configuration, ILogger<SalesQuoteListRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<SalesQuoteList>> GetAllAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? salesQuoteNumber = null,
        string? quoteType = null,
        string? customer = null,
        string? contact = null,
        string? salesPerson = null,
        string? salesQuoteStatus = null)
    {
        try
        {
            var normalizedFromDate = NormalizeDate(fromDate);
            var normalizedToDateExclusive = NormalizeDate(toDate)?.AddDays(1);
            var normalizedSalesQuoteNumber = NormalizeText(salesQuoteNumber);
            var normalizedQuoteType = NormalizeText(quoteType);
            var normalizedCustomer = NormalizeText(customer);
            var normalizedContact = NormalizeText(contact);
            var normalizedSalesPerson = NormalizeText(salesPerson);
            var normalizedSalesQuoteStatus = NormalizeText(salesQuoteStatus);

            if (normalizedFromDate.HasValue && normalizedToDateExclusive.HasValue && normalizedToDateExclusive <= normalizedFromDate)
            {
                throw new ArgumentException("'toDate' must be greater than or equal to 'fromDate'.");
            }

            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteQueryAsync<SalesQuoteList>(
                @"SELECT
                sq.[SalesQuoteID],
                    sq.[salesquotetype] + ' ' + sq.[Direction] AS [QuoteType],
                    sq.[SalesQuoteNumber],
                    sq.[EnqReceivedDate],
                    sq.[SalesQuoteDate] AS [QuoteSentOn],
                    sq.[SalesPersonDisplayName],
                    sq.[CompanyDisplayName] AS [Customer],
                    sq.[ContactDisplayName] AS [Contact],
                    sq.[GrossWeight],
                    sq.[Volume],
                    sq.[Commodity],
                    sq.[RequiredEquipment],
                    sq.[ApprovedDate] AS [BusinessReceivedDate],
                    sn.[AgentName],
                    sq.[ContactID],
                    sq.[SalesQuoteStatus],
                    sq.[PreparedByDislayName],
                    DATEDIFF(DAY, sq.[EnqReceivedDate], sq.[SalesQuoteDate]) AS [quote_days],
                    DATEDIFF(DAY, sq.[SalesQuoteDate], sq.[ApprovedDate]) AS [Response_days]
                  FROM [SalesQuote_New] sq
                  JOIN [SalesQuoteNomDetails] sn ON sq.[SalesQuoteID] = sn.[SQID]
                  WHERE (@FromDate IS NULL OR sq.[SalesQuoteDate] >= @FromDate)
                    AND (@ToDateExclusive IS NULL OR sq.[SalesQuoteDate] < @ToDateExclusive)
                    AND (@SalesQuoteNumberLike IS NULL OR sq.[SalesQuoteNumber] LIKE @SalesQuoteNumberLike)
                    AND (@QuoteTypeLike IS NULL OR (sq.[salesquotetype] + ' ' + sq.[Direction]) LIKE @QuoteTypeLike)
                    AND (@CustomerLike IS NULL OR sq.[CompanyDisplayName] LIKE @CustomerLike)
                    AND (@ContactLike IS NULL OR sq.[ContactDisplayName] LIKE @ContactLike)
                    AND (@SalesPersonLike IS NULL OR sq.[SalesPersonDisplayName] LIKE @SalesPersonLike)
                    AND (@SalesQuoteStatusLike IS NULL OR sq.[SalesQuoteStatus] LIKE @SalesQuoteStatusLike)
                  ORDER BY sq.[SalesQuoteDate] DESC, sq.[SalesQuoteID] DESC",
                new
                {
                    FromDate = normalizedFromDate,
                    ToDateExclusive = normalizedToDateExclusive,
                    SalesQuoteNumberLike = normalizedSalesQuoteNumber is null ? null : $"%{normalizedSalesQuoteNumber}%",
                    QuoteTypeLike = normalizedQuoteType is null ? null : $"%{normalizedQuoteType}%",
                    CustomerLike = normalizedCustomer is null ? null : $"%{normalizedCustomer}%",
                    ContactLike = normalizedContact is null ? null : $"%{normalizedContact}%",
                    SalesPersonLike = normalizedSalesPerson is null ? null : $"%{normalizedSalesPerson}%",
                    SalesQuoteStatusLike = normalizedSalesQuoteStatus is null ? null : $"%{normalizedSalesQuoteStatus}%"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving SalesQuote list.");
            throw;
        }
    }

    public async Task<vw_cargoMint_salesQuote_Detail?> GetByIdAsync(int salesQuoteId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
                        var results = await connection.ExecuteQueryAsync<GeneratedModels.vw_cargoMint_salesQuote_Detail>(
                            @"SELECT
                                v.SalesQuoteID,
                                v.QuoteType,
                                v.SalesQuoteNumber,
                                v.EnqReceivedDate,
                                v.QuoteSentOn,
                                v.SalesPersonDisplayName,
                                v.Customer,
                                v.Contact,
                                v.GrossWeight,
                                v.Volume,
                                v.Commodity,
                                v.RequiredEquipment,
                                v.BusinessReceivedDate,
                                v.AgentName,
                                v.ContactID,
                                v.SalesQuoteStatus,
                                v.PreparedByDislayName,
                                v.NominationType,
                                v.RoofAgent,
                                v.Modeoftransport,
                                v.Direction,
                                v.CompanyAddress,
                                v.ChargeableWeight,
                                v.quote_days AS QuoteDays,
                                v.Response_days AS ResponseDays
                              FROM vw_cargoMint_salesQuote_Detail v
                              WHERE v.SalesQuoteID = @SalesQuoteID",
                            new { SalesQuoteID = salesQuoteId });

                        return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving SalesQuote detail for id {SalesQuoteId}.", salesQuoteId);
            throw;
        }
    }

    public async Task<IEnumerable<GeneratedModels.SalesQuoteDetails_New>> GetDetailsBySalesQuoteIdAsync(int salesQuoteId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            var results = await connection.ExecuteQueryAsync<GeneratedModels.SalesQuoteDetails_New>(
                @"SELECT *
                  FROM SalesQuoteDetails_New s
                  WHERE s.SalesQuoteID = @SalesQuoteID
                  ORDER BY s.SortOrder",
                new { SalesQuoteID = salesQuoteId });

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving SalesQuote details for id {SalesQuoteId}.", salesQuoteId);
            throw;
        }
    }

    public async Task<IEnumerable<GeneratedModels.SalesQuoteCharges_New>> GetIncomeChargesBySalesQuoteIdAsync(int salesQuoteId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            var results = await connection.ExecuteQueryAsync<GeneratedModels.SalesQuoteCharges_New>(
                @"SELECT *
                  FROM SalesQuoteCharges_New s
                  WHERE s.SQID = @SalesQuoteID
                    AND s.isDeleted = 0
                    AND s.IncomeExpense = 'INCOME'",
                new { SalesQuoteID = salesQuoteId });

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving SalesQuote income charges for id {SalesQuoteId}.", salesQuoteId);
            throw;
        }
    }

    private static DateTime? NormalizeDate(DateTime? date)
    {
        if (!date.HasValue)
        {
            return null;
        }

        var normalized = date.Value.Date;
        return normalized == DateTime.MinValue.Date ? null : normalized;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
