using GeneratedModels;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace RepoDbApi.Repositories;

public class AllCargoListRepository : IAllCargoListRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AllCargoListRepository> _logger;

    public AllCargoListRepository(IConfiguration configuration, ILogger<AllCargoListRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<IEnumerable<AllCargoList>> GetAllAsync(
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        string? job = null,
        string? cycle = null)
    {
        try
        {
            var normalizedJob = NormalizeText(job);
            var normalizedCycle = NormalizeText(cycle);

            // Apply date range only if one or both dates are provided. If neither is provided,
            // leave the date parameters null so the SQL will not filter by date.
            DateTime? normalizedFrom = NormalizeDate(createdFrom);
            DateTime? normalizedTo = NormalizeDate(createdTo);

            DateTime? fromDate = null;
            DateTime? toDateExclusive = null;

            if (normalizedFrom.HasValue || normalizedTo.HasValue)
            {
                fromDate = normalizedFrom?.Date;
                toDateExclusive = normalizedTo?.Date.AddDays(1);

                fromDate ??= toDateExclusive!.Value.AddYears(-1);
                toDateExclusive ??= fromDate.Value.AddYears(1);

                if (toDateExclusive.Value <= fromDate.Value)
                {
                    throw new ArgumentException("'createdTo' must be greater than or equal to 'createdFrom'.");
                }

                var maxToDateExclusive = fromDate.Value.AddYears(1);
                if (toDateExclusive.Value > maxToDateExclusive)
                {
                    _logger.LogInformation(
                        "Date range exceeded one year. Clamping range from {FromDate} - {ToDateExclusive} to {FromDate} - {MaxToDateExclusive}.",
                        fromDate.Value,
                        toDateExclusive.Value,
                        maxToDateExclusive);
                    toDateExclusive = maxToDateExclusive;
                }
            }
            else
            {
                // If no date filters supplied, apply a conservative default range (current month)
                // to avoid unbounded queries that may time out.
                var range = ResolveDateRange(createdFrom, createdTo);
                fromDate = range.fromDate;
                toDateExclusive = range.toDateExclusive;
                _logger.LogInformation("No date filters provided for AllCargoList; applying default range {FromDate} - {ToDateExclusive}.", fromDate, toDateExclusive);
            }

            await using var connection = new SqlConnection(_connectionString);
                        const string sql = @"SELECT
                                                                     TRY_CONVERT(int, [CargoID]) AS [CargoID],
                                                                     CAST([CYCLE] AS nvarchar(100)) AS [Cycle],
                                                                     CAST([invoiceno] AS nvarchar(200)) AS [invoiceno],
                                                                     CAST([BookingNumber] AS nvarchar(200)) AS [BookingNumber],
                                                                     TRY_CONVERT(datetime, [BookingDate]) AS [BookingDate],
                                                                     CAST([CustomerName] AS nvarchar(500)) AS [CustomerName],
                                                                     CAST([Shipper] AS nvarchar(500)) AS [Shipper],
                                                                     CAST([Consignee] AS nvarchar(500)) AS [Consignee],
                                                                     CAST([ModeOfTransport] AS nvarchar(100)) AS [ModeOfTransport],
                                                                     CAST([TransportDirection] AS nvarchar(100)) AS [TransportDirection],
                                                                     CAST([POL] AS nvarchar(200)) AS [POL],
                                                                     CAST([POD] AS nvarchar(200)) AS [POD],
                                                                     TRY_CONVERT(datetime, [ETD]) AS [ETD],
                                                                     TRY_CONVERT(datetime, [ETA]) AS [ETA],
                                                                     CAST([JobNo] AS nvarchar(200)) AS [JobNo],
                                                                     CAST([OriginAgent] AS nvarchar(500)) AS [OriginAgent],
                                                                     CAST([DestinationAgent] AS nvarchar(500)) AS [DestinationAgent],
                                                                     TRY_CONVERT(int, [20GP]) AS [20GP],
                                                                     TRY_CONVERT(int, [20HC]) AS [20HC],
                                                                     TRY_CONVERT(int, [40GP]) AS [40GP],
                                                                     TRY_CONVERT(int, [40HC]) AS [40HC],
                                                                     CAST([ContainerNo] AS nvarchar(500)) AS [ContainerNo],
                                                                     CAST([INVOICE_NUMBER] AS nvarchar(200)) AS [INVOICE_NUMBER],
                                                                     CAST([OceanLine] AS nvarchar(300)) AS [OceanLine],
                                                                     CAST([Vessel] AS nvarchar(300)) AS [Vessel],
                                                                     CAST([MasterNo] AS nvarchar(200)) AS [MasterNo],
                                                                     CAST([HouseNo] AS nvarchar(200)) AS [HouseNo],
                                                                     CAST([CreatedBys] AS nvarchar(200)) AS [CreatedBys],
                                                                     TRY_CONVERT(datetime, [DateCreated]) AS [DateCreated],
                                                                     CAST([ModifiedBys] AS nvarchar(200)) AS [ModifiedBys],
                                                                     TRY_CONVERT(datetime, [DateModified]) AS [DateModified],
                                                                     TRY_CONVERT(int, [OfficeID]) AS [OfficeID],
                                                                     CAST([WebJob] AS nvarchar(100)) AS [WebJob]
                                                         FROM [dbo].[vw_AllCargoList]
                                                         WHERE (@FromDate IS NULL OR TRY_CONVERT(datetime, [DateCreated]) >= @FromDate)
                                                             AND (@ToDateExclusive IS NULL OR TRY_CONVERT(datetime, [DateCreated]) < @ToDateExclusive)
                                                             AND (@JobLike IS NULL OR CAST([JobNo] AS nvarchar(200)) LIKE @JobLike)
                                                             AND (@CycleLike IS NULL OR CAST([CYCLE] AS nvarchar(100)) LIKE @CycleLike)
                                                         ORDER BY [CargoID] DESC
";
            return await connection.ExecuteQueryAsync<AllCargoList>(
                sql,
                new
                {
                    FromDate = fromDate,
                    ToDateExclusive = toDateExclusive,
                    JobLike = normalizedJob is null ? null : $"%{normalizedJob}%",
                    CycleLike = normalizedCycle is null ? null : $"%{normalizedCycle}%"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving all AllCargoList records.");
            throw;
        }
    }

    private (DateTime fromDate, DateTime toDateExclusive) ResolveDateRange(DateTime? createdFrom, DateTime? createdTo)
    {
        DateTime? normalizedFrom = NormalizeDate(createdFrom);
        DateTime? normalizedTo = NormalizeDate(createdTo);

        DateTime? fromDate = normalizedFrom?.Date;
        DateTime? toDateExclusive = normalizedTo?.Date.AddDays(1);

        if (!fromDate.HasValue && !toDateExclusive.HasValue)
        {
            var today = DateTime.Today;
            fromDate = new DateTime(today.Year, today.Month, 1);
            toDateExclusive = fromDate.Value.AddMonths(1);
        }
        else
        {
            fromDate ??= toDateExclusive!.Value.AddYears(-1);
            toDateExclusive ??= fromDate.Value.AddYears(1);
        }

        if (toDateExclusive.Value <= fromDate.Value)
        {
            throw new ArgumentException("'createdTo' must be greater than or equal to 'createdFrom'.");
        }

        var maxToDateExclusive = fromDate.Value.AddYears(1);
        if (toDateExclusive.Value > maxToDateExclusive)
        {
            _logger.LogInformation(
                "Date range exceeded one year. Clamping range from {FromDate} - {ToDateExclusive} to {FromDate} - {MaxToDateExclusive}.",
                fromDate.Value,
                toDateExclusive.Value,
                maxToDateExclusive);
            toDateExclusive = maxToDateExclusive;
        }

        return (fromDate.Value, toDateExclusive.Value);
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var date = value.Value.Date;
        return date == DateTime.MinValue.Date ? null : date;
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
