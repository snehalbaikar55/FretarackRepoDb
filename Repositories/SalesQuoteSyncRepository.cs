using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using RepoDb;
using RepoDbApi.Contracts;
using GeneratedModels;
using RepoDbApi.Services;

namespace RepoDbApi.Repositories;

public interface ISalesQuoteSyncRepository
{
    Task<SalesQuoteSyncResponse> SyncAsync(string number, int orgId, bool dryRun, CancellationToken cancellationToken);
}

public sealed class SalesQuoteSyncRepository : ISalesQuoteSyncRepository
{
    private readonly string sourceString;
    private readonly string targetString;
    private readonly int timeout;

    public SalesQuoteSyncRepository(IConfiguration configuration)
    {
        sourceString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
        targetString = configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is required.");
        timeout = configuration.GetValue<int?>("SalesQuoteSync:CommandTimeoutSeconds") ?? 120;
        if (timeout <= 0) throw new InvalidOperationException("Command timeout must be positive.");
    }

    public async Task<SalesQuoteSyncResponse> SyncAsync(string number, int orgId, bool dryRun, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Length > 20)
            throw new ArgumentException("A sales quote number of 1–20 characters is required.");
        if (orgId <= 0) throw new ArgumentException("A positive OrgId is required.");

        List<SalesQuote_New> headers;
        List<SalesQuoteDetails_New> details;
        List<SalesQuoteNomDetails> nominations;
        List<SalesQuoteCharges_New> charges;
        await using (var source = new SqlConnection(sourceString))
        {
            await source.OpenAsync(ct);
            using var readTransaction = source.BeginTransaction(IsolationLevel.Serializable);
            headers = await ReadAsync<SalesQuote_New>(source, readTransaction, "SalesQuote_New", "SalesQuoteNumber", number, "SalesQuoteID", ct);
            if (headers.Count == 0) throw new KeyNotFoundException("Sales quotation was not found in Fretrack.");
            if (headers.Count != 1) throw new SalesQuoteSyncConflictException("Sales quote number matches multiple source records.");
            var header = headers[0];
            if (header.isDeleted) throw new SalesQuoteSyncConflictException("Source sales quotation is deleted.");
            details = await ReadAsync<SalesQuoteDetails_New>(source, readTransaction, "SalesQuoteDetails_New", "SalesQuoteID", header.SalesQuoteID, "SalesQuoteDetailID", ct);
            nominations = await ReadAsync<SalesQuoteNomDetails>(source, readTransaction, "SalesQuoteNomDetails", "SQID", header.SalesQuoteID, "SqNomDetailID", ct);
            charges = await ReadAsync<SalesQuoteCharges_New>(source, readTransaction, "SalesQuoteCharges_New", "SQID", header.SalesQuoteID, "SQChargeID", ct);
            var detailIDs = details.Select(x => x.SalesQuoteDetailID).ToHashSet();
            if (charges.Any(x => x.SQDetailRowID.HasValue && !detailIDs.Contains(x.SQDetailRowID.Value)))
                throw new SalesQuoteSyncConflictException("A charge references a detail outside this quotation.");
            EnsureUnique(details, x => x.SalesQuoteDetailID);
            EnsureUnique(nominations, x => x.SqNomDetailID);
            EnsureUnique(charges, x => x.SQChargeID);
            await readTransaction.CommitAsync(ct);
        }

        var id = headers[0].SalesQuoteID;
        var counts = new Dictionary<string, int>
        {
            ["Fretrack_SalesQuote_New"] = headers.Count,
            ["Fretrack_SalesQuoteDetails_New"] = details.Count,
            ["Fretrack_SalesQuoteNomDetails"] = nominations.Count,
            ["Fretrack_SalesQuoteCharges_New"] = charges.Count
        };
        await using var target = new SqlConnection(targetString);
        await target.OpenAsync(ct);
        using var transaction = target.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var lockResults = await target.ExecuteQueryAsync<int>(
                "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'FretrackSalesQuoteSync:v1', " +
                "@LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; SELECT @r;",
                transaction: transaction, commandTimeout: timeout, cancellationToken: ct);
            if (lockResults.Single() < 0)
                throw new SalesQuoteSyncConflictException("Another sales quote sync is running. Retry shortly.");

            var oldHeaders = await ReadAsync<SalesQuote_New>(target, transaction, "Fretrack_SalesQuote_New", "SalesQuoteID", id, "SalesQuoteID", ct);
            var oldDetails = await ReadAsync<SalesQuoteDetails_New>(target, transaction, "Fretrack_SalesQuoteDetails_New", "SalesQuoteID", id, "SalesQuoteDetailID", ct);
            var oldNominations = await ReadAsync<SalesQuoteNomDetails>(target, transaction, "Fretrack_SalesQuoteNomDetails", "SQID", id, "SqNomDetailID", ct);
            var oldCharges = await ReadAsync<SalesQuoteCharges_New>(target, transaction, "Fretrack_SalesQuoteCharges_New", "SQID", id, "SQChargeID", ct);
            if (oldHeaders.Count + oldDetails.Count + oldNominations.Count + oldCharges.Count > 0)
            {
                if (!Same(headers, oldHeaders) || !Same(details, oldDetails) ||
                    !Same(nominations, oldNominations) || !Same(charges, oldCharges))
                    throw new SalesQuoteSyncConflictException("Conflicting or partial staging data exists. No records were changed.");
                await UpdateMappingsAsync(target, transaction, id, orgId, ct);
                var existingQuotation = await ReadPrefillAsync(target, transaction, id, orgId, ct);
                if (dryRun) await transaction.RollbackAsync(ct);
                else await transaction.CommitAsync(ct);
                return new(number, id, "already_synced", dryRun, false, counts)
                { OrgId = orgId, StagingMappingsUpdated = !dryRun, Quotation = existingQuotation };
            }
            var numberCount = await target.ExecuteQueryAsync<int>(
                "SELECT COUNT(*) FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteNumber=@Number",
                new { Number = number }, transaction: transaction, commandTimeout: timeout, cancellationToken: ct);
            if (numberCount.Single() > 0)
                throw new SalesQuoteSyncConflictException("Quote number already exists under another staged ID.");

            await CopyAsync(target, transaction, "Fretrack_SalesQuote_New", "SalesQuoteID", headers, ct);
            await CopyAsync(target, transaction, "Fretrack_SalesQuoteDetails_New", "SalesQuoteDetailID", details, ct);
            await CopyAsync(target, transaction, "Fretrack_SalesQuoteNomDetails", "SqNomDetailID", nominations, ct);
            await CopyAsync(target, transaction, "Fretrack_SalesQuoteCharges_New", "SQChargeID", charges, ct);
            await UpdateMappingsAsync(target, transaction, id, orgId, ct);
            var quotation = await ReadPrefillAsync(target, transaction, id, orgId, ct);
            if (dryRun) await transaction.RollbackAsync(ct);
            else await transaction.CommitAsync(ct);
            return new(number, id, dryRun ? "validated" : "synced", dryRun, false, counts)
            { OrgId = orgId, StagingMappingsUpdated = !dryRun, Quotation = quotation };
        }
        catch
        {
            // Dispose also rolls back if the connection failed before rollback can complete.
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception) { /* Preserve the original exception. */ }
            throw;
        }
    }

    private Task<SalesQuotePrefillData> ReadPrefillAsync(SqlConnection connection, SqlTransaction transaction,
        int id, int orgId, CancellationToken ct) =>
        AtStepAsync("prefill", () => ReadPrefillCoreAsync(connection, transaction, id, orgId, ct));

    private async Task<SalesQuotePrefillData> ReadPrefillCoreAsync(SqlConnection connection, SqlTransaction transaction,
        int id, int orgId, CancellationToken ct)
    {
        // Read AFTER mapping updates, BEFORE commit/rollback: dry runs also get a preview.
        var header = (await ReadAsync<MappedSalesQuoteHeader>(connection, transaction,
            "Fretrack_SalesQuote_New", "SalesQuoteID", id, "SalesQuoteID", ct)).Single();
        var details = await ReadAsync<MappedSalesQuoteDetail>(connection, transaction,
            "Fretrack_SalesQuoteDetails_New", "SalesQuoteID", id, "SalesQuoteDetailID", ct);
        var nominations = await ReadAsync<SalesQuoteNomDetails>(connection, transaction,
            "Fretrack_SalesQuoteNomDetails", "SQID", id, "SqNomDetailID", ct);
        var charges = await ReadAsync<MappedSalesQuoteCharge>(connection, transaction,
            "Fretrack_SalesQuoteCharges_New", "SQID", id, "SQChargeID", ct);
        async Task<List<T>> Query<T>(string sql, object? parameters = null) where T : class =>
            (await connection.ExecuteQueryAsync<T>(sql, parameters, transaction: transaction,
                commandTimeout: timeout, cancellationToken: ct)).ToList();
        var refs = new SalesQuotePrefillReferences
        {
            Currencies = await Query<SalesQuoteCurrencyReference>(
                "SELECT CurrencyId,CurrencyCode FROM dbo.Currencies WHERE ISNULL(IsDeleted,0)=0"),
            Units = await Query<SalesQuoteNamedReference>(
                "SELECT ApplyPerId Id,ApplyPerName Name FROM dbo.ApplyPer WHERE (OrgId=@OrgId OR OrgId IS NULL) AND IsDeleted=0 AND IsActive=1", new { OrgId = orgId }),
            Taxes = await Query<SalesQuoteTaxReference>(
                "SELECT TaxGroupId,TotalRate FROM dbo.TaxGroups WHERE (OrgId=@OrgId OR OrgId IS NULL) AND ISNULL(IsDeleted,0)=0 AND IsActive=1", new { OrgId = orgId }),
            Companies = await Query<SalesQuoteNamedReference>("""
                SELECT CompanyId Id,CompanyName Name FROM dbo.Companies
                WHERE OrgId=@OrgId AND ISNULL(IsDeleted,0)=0 AND CompanyId IN
                (SELECT CargoMintCompanyID FROM dbo.Fretrack_SalesQuote_New WHERE SalesQuoteID=@ID
                 UNION SELECT CargoMintPayingPartyID FROM dbo.Fretrack_SalesQuoteCharges_New WHERE SQID=@ID)
                """, new { OrgId = orgId, ID = id })
        };
        var canonicalType = SalesQuoteServiceNames.Normalize(header.SalesQuoteType, header.Modeoftransport);
        const string serviceModeSql = """
            SELECT DISTINCT CargoMintServiceTypeId,CargoMintTransportModeId
            FROM dbo.Fretrack_Service_Mode_Mapping
            WHERE UPPER(LTRIM(RTRIM(JobTypeName)))=UPPER(LTRIM(RTRIM(@QuoteType)))
              AND UPPER(LTRIM(RTRIM(ModeOfTransport)))=UPPER(LTRIM(RTRIM(@Mode)))
            """;
        var modes = await Query<SalesQuoteServiceModeReference>(serviceModeSql,
            new { QuoteType = header.SalesQuoteType, Mode = header.Modeoftransport });
        // Preserve exact Fretrack mappings; fall back to the base master name
        // when only a mapping such as FCL/LCL (without direction) exists.
        if (modes.Count == 0 && canonicalType is not null &&
            !canonicalType.Equals(header.SalesQuoteType?.Trim(), StringComparison.OrdinalIgnoreCase))
            modes = await Query<SalesQuoteServiceModeReference>(serviceModeSql,
                new { QuoteType = canonicalType, Mode = header.Modeoftransport });
        var masterServices = await Query<SalesQuoteNamedReference>("""
            SELECT ServiceTypeId Id,ServiceType Name FROM dbo.ServiceTypes
            WHERE (OrgId=@OrgId OR OrgId IS NULL) AND ISNULL(IsDeleted,0)=0 AND IsActive=1
            """, new { OrgId = orgId });
        if (modes.Count == 1)
        {
            refs.ModeId = modes[0].CargoMintTransportModeId;
            var mapped = masterServices.SingleOrDefault(x => x.Id == modes[0].CargoMintServiceTypeId);
            var mappedName = SalesQuoteServiceNames.MasterName(mapped?.Name);
            if (mapped is not null && mappedName is not null && (canonicalType is null || mappedName == canonicalType))
            {
                refs.ServiceTypeId = mapped.Id;
                refs.ServiceTypeName = mappedName;
            }
        }
        if (refs.ServiceTypeId is null && canonicalType is not null)
        {
            var matches = masterServices.Where(x => SalesQuoteServiceNames.MasterName(x.Name) == canonicalType).ToList();
            if (matches.Count == 1) { refs.ServiceTypeId = matches[0].Id; refs.ServiceTypeName = canonicalType; }
        }
        var directions = await Query<SalesQuoteNamedReference>("""
            SELECT DirectionId Id,DirectionName Name FROM dbo.TransportDirections
            WHERE (OrgId=@OrgId OR OrgId IS NULL) AND ISNULL(IsDeleted,0)=0 AND IsActive=1
              AND (UPPER(LTRIM(RTRIM(DirectionName)))=UPPER(LTRIM(RTRIM(@Direction)))
                   OR UPPER(LTRIM(RTRIM(DirectionCode)))=UPPER(LTRIM(RTRIM(@Direction))))
            """, new { OrgId = orgId, Direction = header.Direction });
        if (directions.Count == 1) refs.DirectionId = directions[0].Id;
        // Same local-currency convention as CargoMint QuotationFinancials (settings schema).
        var localCodes = await connection.ExecuteQueryAsync<string>("""
            SELECT TOP (1) DefaultCurrencyCode FROM settings.OrgSettings
            WHERE OrgId=@OrgId AND ISNULL(IsDeleted,0)=0 ORDER BY COALESCE(UpdatedDate,CreatedDate) DESC
            """, new { OrgId = orgId }, transaction: transaction, commandTimeout: timeout, cancellationToken: ct);
        var localCode = localCodes.FirstOrDefault()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(localCode)) localCode = "INR";
        var local = refs.Currencies.Where(x => x.CurrencyCode.Trim().Equals(localCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (local.Count == 1) refs.LocalCurrencyId = local[0].CurrencyId;
        var commodities = await Query<SalesQuoteNamedReference>("""
            SELECT CommodityId Id,CommodityName Name FROM dbo.CommodityMaster
            WHERE (OrgId=@OrgId OR OrgId IS NULL) AND ISNULL(IsDeleted,0)=0 AND IsActive=1
              AND LTRIM(RTRIM(CommodityName))=LTRIM(RTRIM(@Commodity))
            """, new { OrgId = orgId, Commodity = header.Commodity });
        if (commodities.Count == 1) refs.CommodityId = commodities[0].Id;
        return SalesQuotePrefillBuilder.Build(header, details, nominations, charges, refs, orgId);
    }

    private async Task UpdateMappingsAsync(SqlConnection connection, SqlTransaction transaction,
        int salesQuoteId, int orgId, CancellationToken ct)
    {
        await AtStepAsync("staging.mappings", () => connection.ExecuteNonQueryAsync(
            "dbo.usp_UpdateSalesQuoteStagingMappings",
            new { SalesQuoteID = salesQuoteId, OrgId = orgId },
            commandType: CommandType.StoredProcedure, transaction: transaction,
            commandTimeout: timeout, cancellationToken: ct));
    }

    private async Task<List<T>> ReadAsync<T>(SqlConnection connection, SqlTransaction transaction,
        string table, string filter, object value, string key, CancellationToken ct) where T : class
    {
        // All SQL identifiers come from internal constants or model properties, never requests.
        var columns = string.Join(",", Properties<T>().Select(p => $"[{p.Name}]"));
        var result = await AtStepAsync("read." + table, () => connection.ExecuteQueryAsync<T>(
            $"SELECT {columns} FROM [dbo].[{table}] WHERE [{filter}]=@Value ORDER BY [{key}]",
            new { Value = value }, transaction: transaction, commandTimeout: timeout, cancellationToken: ct));
        return result.ToList();
    }

    private async Task CopyAsync<T>(SqlConnection connection, SqlTransaction transaction,
        string table, string key, List<T> rows, CancellationToken ct) where T : class
    {
        if (rows.Count == 0) return;
        var properties = Properties<T>();
        var keyProperty = properties.Single(p => p.Name == key);
        foreach (var row in rows)
        {
            var count = await connection.ExecuteQueryAsync<int>(
                $"SELECT COUNT(*) FROM [dbo].[{table}] WHERE [{key}]=@Value",
                new { Value = keyProperty.GetValue(row) }, transaction: transaction,
                commandTimeout: timeout, cancellationToken: ct);
            if (count.Single() > 0)
                throw new SalesQuoteSyncConflictException($"An ID already exists in {table} for another quotation.");
        }
        var data = new DataTable();
        foreach (var property in properties)
            data.Columns.Add(property.Name, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
        foreach (var row in rows)
            data.Rows.Add(properties.Select(p => p.GetValue(row) ?? DBNull.Value).ToArray());

        // Same SqlBulkCopy staging strategy used by the existing single-job repository.
        // Only source properties are mapped; CargoMint mapping IDs remain NULL.
        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls, transaction)
        {
            DestinationTableName = $"[dbo].[{table}]", BulkCopyTimeout = timeout, BatchSize = 500
        };
        foreach (var property in properties) bulk.ColumnMappings.Add(property.Name, property.Name);
        await bulk.WriteToServerAsync(data, ct);
    }

    private static async Task<T> AtStepAsync<T>(string stage, Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not (SalesQuoteSyncStepException or SalesQuoteSyncConflictException or KeyNotFoundException or OperationCanceledException))
        {
            throw new SalesQuoteSyncStepException(stage, ex);
        }
    }

    private static PropertyInfo[] Properties<T>() => typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    internal static bool Same<T>(List<T> left, List<T> right)
    {
        if (left.Count != right.Count) return false;
        var properties = Properties<T>();
        return left.Zip(right).All(pair => properties.All(p => Equals(p.GetValue(pair.First), p.GetValue(pair.Second))));
    }

    private static void EnsureUnique<T>(List<T> rows, Func<T, int> key)
    {
        if (rows.Select(key).Distinct().Count() != rows.Count)
            throw new SalesQuoteSyncConflictException("Duplicate source IDs were found.");
    }
}
