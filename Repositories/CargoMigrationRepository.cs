using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public class CargoMigrationRepository : ICargoMigrationRepository
{
    private readonly string _fretrackConnectionString;
    private readonly string _cargoMintConnectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly ICargoDocumentMigrationRepository _cargoDocumentMigrationRepository;
    private readonly ILogger<CargoMigrationRepository> _logger;

    public CargoMigrationRepository(
        IConfiguration configuration,
        ICargoDocumentMigrationRepository cargoDocumentMigrationRepository,
        ILogger<CargoMigrationRepository> logger)
    {
        _fretrackConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        _cargoMintConnectionString = configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        _commandTimeoutSeconds = configuration.GetValue<int?>("CargoMigration:CommandTimeoutSeconds")
            ?? configuration.GetValue<int?>("Migration:CommandTimeoutSeconds")
            ?? 600;
        _cargoDocumentMigrationRepository = cargoDocumentMigrationRepository;
        _logger = logger;
    }

    public async Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, int orgId = 18, CancellationToken cancellationToken = default)
    {
        var migrationStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Single-job repository migration started for JobNo {JobNo}.", jobNo);

        if (string.IsNullOrWhiteSpace(jobNo))
        {
            throw new ArgumentException("JobNo is required.", nameof(jobNo));
        }

        await using var sourceConnection = new SqlConnection(_fretrackConnectionString);
        await using var targetConnection = new SqlConnection(_cargoMintConnectionString);

        await sourceConnection.OpenAsync(cancellationToken);
        await targetConnection.OpenAsync(cancellationToken);
        _logger.LogInformation(
            "Single-job migration connections opened for JobNo {JobNo} in {ElapsedMilliseconds} ms.",
            jobNo,
            migrationStopwatch.ElapsedMilliseconds);

        var cargoSchema = await ResolveTableSchemaAsync(sourceConnection, "Cargo", cancellationToken, defaultSchema: "dbo");
        string? cargoMintCargoSchema = null;
        try
        {
            cargoMintCargoSchema = await ResolveTableSchemaAsync(targetConnection, "Cargo", cancellationToken, defaultSchema: string.Empty);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Cargo table schema could not be resolved in CargoMintDB. Existence check will be skipped.");
        }

        string? usersSchema = null;
        try
        {
            usersSchema = await ResolveTableSchemaAsync(targetConnection, "Users", cancellationToken, defaultSchema: string.Empty);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Users table schema could not be resolved in CargoMintDB. User validation will be skipped.");
        }

        var cargoHeader = await LoadDataTableAsync(
            sourceConnection,
            BuildCargoHeaderSql(cargoSchema),
            parameters =>
            {
                parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });
                parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
            },
            cancellationToken);
        _logger.LogInformation(
            "Single-job cargo header loaded for JobNo {JobNo} in {ElapsedMilliseconds} ms.",
            jobNo,
            migrationStopwatch.ElapsedMilliseconds);

        if (cargoHeader.Rows.Count == 0)
        {
            throw new KeyNotFoundException($"No cargo record found for JobNo '{jobNo}'.");
        }

        string? salesQuotationSchema = null;
        try
        {
            salesQuotationSchema = await ResolveTableSchemaAsync(targetConnection, "SalesQuotations", cancellationToken, defaultSchema: string.Empty);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "SalesQuotations table schema could not be resolved in CargoMintDB. Quotation validation will be skipped.");
        }

        var cargoId = Convert.ToInt32(cargoHeader.Rows[0]["CargoID"]);
        var normalizedJobNo = Convert.ToString(cargoHeader.Rows[0]["JobNo"]) ?? jobNo;
        var cargoExistsInCargoMint = false;
        if (!string.IsNullOrWhiteSpace(cargoMintCargoSchema))
        {
            try
            {
                cargoExistsInCargoMint = await CargoExistsInCargoMintAsync(
                    targetConnection,
                    cargoMintCargoSchema,
                    cargoId,
                    normalizedJobNo,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cargo existence check in CargoMintDB failed. Continuing migration.");
            }
        }

        using var transaction = targetConnection.BeginTransaction();

        var stagingCommitted = false;

        try
        {
            await DeleteExistingStagingRowsAsync(targetConnection, transaction, cargoId, cancellationToken);

            var stagingDataStopwatch = Stopwatch.StartNew();
            var stagingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Fretrack_Cargo_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_Cargo_Staging",
                    BuildCargoHeaderSql(cargoSchema),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    beforeBulkCopyAsync: async (table, _) =>
                    {
                        await ClearInvalidUserReferencesAsync(
                            table,
                            targetConnection,
                            transaction,
                            usersSchema,
                            cancellationToken,
                            "CargoMintCreatedById",
                            "CargoMintHandledById");

                        if (table.Columns.Contains("CargoMintQuotationId"))
                        {
                            var quotationValue = table.Rows[0]["CargoMintQuotationId"];
                            if (quotationValue is not DBNull and not null)
                            {
                                var quotationId = Convert.ToInt32(quotationValue);
                                var quotationExists = false;

                                if (!string.IsNullOrWhiteSpace(salesQuotationSchema))
                                {
                                    try
                                    {
                                        quotationExists = await SalesQuotationExistsInCargoMintAsync(
                                            targetConnection,
                                            transaction,
                                            salesQuotationSchema,
                                            quotationId,
                                            cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Quotation validation failed for CargoID {CargoID}. The quotation will be cleared from staging.", cargoId);
                                    }
                                }

                                if (!quotationExists)
                                {
                                    table.Rows[0]["CargoMintQuotationId"] = DBNull.Value;
                                }
                            }
                        }
                    }),

                ["Fretrack_ShipmentService_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_ShipmentService_Staging",
                    QualifySourceTables(cargoSchema, ShipmentServiceSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    beforeBulkCopyAsync: async (table, _) =>
                    {
                        await ClearInvalidUserReferencesAsync(
                            table,
                            targetConnection,
                            transaction,
                            usersSchema,
                            cancellationToken,
                            "CargoMinCreatedById");
                    }),

                ["Fretrack_ShipmentPackages_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_ShipmentPackages_Staging",
                    QualifySourceTables(cargoSchema, ShipmentPackagesSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_ShipmentContainers_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_ShipmentContainers_Staging",
                    QualifySourceTables(cargoSchema, ShipmentContainersSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_Shipment_Routing_Staging_New"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_Shipment_Routing_Staging_New",
                    QualifySourceTables(cargoSchema, ShipmentRoutingSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_CargoEntities_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_CargoEntities_Staging",
                    QualifySourceTables(cargoSchema, CargoEntitiesSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    beforeBulkCopyAsync: async (table, _) =>
                    {
                        await ClearInvalidUserReferencesAsync(
                            table,
                            targetConnection,
                            transaction,
                            usersSchema,
                            cancellationToken,
                            "CargoMintCreatedBy",
                            "CargoMintModifiedBy",
                            "CargoMintDeletedBy");
                    }),

                ["Fretrack_Invoices_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_Invoices_Staging",
                    QualifySourceTables(cargoSchema, InvoicesSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_InvoiceEsync_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_InvoiceEsync_Staging",
                    QualifySourceTables(cargoSchema, InvoiceESyncSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_VendorBill_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_VendorBill_Staging",
                    QualifySourceTables(cargoSchema, VendorBillSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["VendorBillID"] = "FretrackVendorBillID",
                        ["CargoID"] = "FretrackCargoID",
                        ["PayingPartyID"] = "FretrackPayingPartyID",
                        ["PayingPartyAddressID"] = "FretrackPayingPartyAddressID"
                    }),

                ["Fretrack_ShipmentCharges_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_ShipmentCharges_Staging",
                    QualifySourceTables(cargoSchema, ShipmentChargesSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_InvoiceLineItems_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_InvoiceLineItems_Staging",
                    QualifySourceTables(cargoSchema, InvoiceLineItemsSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_VendorBillLineItems_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_VendorBillLineItems_Staging",
                    QualifySourceTables(cargoSchema, VendorBillLineItemsSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken),

                ["Fretrack_HBL_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_HBL_Staging",
                    QualifySourceTables(cargoSchema, HblSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["HBLID"] = "FretrackHBLID",
                        ["CargoID"] = "FretrackCargoID"
                    }),

                ["Fretrack_CargoDocuments_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "Fretrack_CargoDocuments_Staging",
                    QualifySourceTables(cargoSchema, CargoDocumentsSql),
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                        parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
                    },
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CargoDocumentID"] = "FretrackCargoDocumentID",
                        ["DocumentTypeID"] = "FretrackDocumentTypeID"
                    })
            };
            _logger.LogInformation(
                "Stage job data completed for CargoID {CargoId} in {ElapsedMilliseconds} ms.",
                cargoId,
                stagingDataStopwatch.ElapsedMilliseconds);

            var stagingCommitStopwatch = Stopwatch.StartNew();
            await transaction.CommitAsync(cancellationToken);
            stagingCommitted = true;
            _logger.LogInformation(
                "Single-job staging transaction commit completed for CargoID {CargoId} in {ElapsedMilliseconds} ms.",
                cargoId,
                stagingCommitStopwatch.ElapsedMilliseconds);

            var storedProcedureStopwatch = Stopwatch.StartNew();
            await ExecuteStoredProcedureAsync(targetConnection, cargoId, orgId, cancellationToken);
            _logger.LogInformation(
                "Single-job migration stored procedure completed for CargoID {CargoId} in {ElapsedMilliseconds} ms.",
                cargoId,
                storedProcedureStopwatch.ElapsedMilliseconds);

            var documentMigrationStopwatch = Stopwatch.StartNew();
            var cargoDocumentMigrationResult = await _cargoDocumentMigrationRepository.MigrateByCargoIdAsync(cargoId, orgId, cancellationToken);
            _logger.LogInformation(
                "Cargo document migration completed for CargoID {CargoId} in {ElapsedMilliseconds} ms. Migrated={MigratedRows}, Skipped={SkippedRows}, Failed={FailedRows}.",
                cargoId,
                documentMigrationStopwatch.ElapsedMilliseconds,
                cargoDocumentMigrationResult.MigratedRows,
                cargoDocumentMigrationResult.SkippedRows,
                cargoDocumentMigrationResult.FailedRows);

            return new CargoMigrationResponse
            {
                JobNo = normalizedJobNo,
                CargoId = cargoId,
                CargoExistsInCargoMint = cargoExistsInCargoMint,
                ActualMigrationCompleted = true,
                Status = "success",
                Message = cargoExistsInCargoMint
                    ? "Cargo already exists in CargoMint; staging rows were refreshed and actual tables were updated."
                    : "New cargo staged successfully and actual tables were updated.",
                StagingCounts = stagingCounts
            };
        }
        catch
        {
            if (!stagingCommitted)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task<CargoMigrationResponse> MigrateContainerOnlyAsync(string jobNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobNo))
        {
            throw new ArgumentException("JobNo is required.", nameof(jobNo));
        }

        await using var sourceConnection = new SqlConnection(_fretrackConnectionString);
        await using var targetConnection = new SqlConnection(_cargoMintConnectionString);

        await sourceConnection.OpenAsync(cancellationToken);
        await targetConnection.OpenAsync(cancellationToken);

        var cargoSchema = await ResolveTableSchemaAsync(sourceConnection, "Cargo", cancellationToken, defaultSchema: "dbo");
        var cargoContainersSchema = await ResolveTableSchemaAsync(sourceConnection, "CargoContainers", cancellationToken, defaultSchema: "dbo");

        var cargoHeader = await LoadDataTableAsync(
            sourceConnection,
            BuildCargoLookupSql(cargoSchema),
            parameters =>
            {
                parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });
            },
            cancellationToken);

        if (cargoHeader.Rows.Count == 0)
        {
            throw new KeyNotFoundException($"No cargo record found for JobNo '{jobNo}'.");
        }

        var cargoId = Convert.ToInt32(cargoHeader.Rows[0]["CargoID"]);
        var normalizedJobNo = Convert.ToString(cargoHeader.Rows[0]["JobNo"]) ?? jobNo;

        using var transaction = targetConnection.BeginTransaction();
        var stagingCommitted = false;

        try
        {
            await ExecuteNonQueryAsync(
                targetConnection,
                transaction,
                "DELETE FROM Fretrack_ShipmentContainers_Staging WHERE FretrackCargoId = @CargoID",
                parameters =>
                {
                    parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                },
                cancellationToken);

            var containerCount = await CopyQueryToStagingAsync(
                sourceConnection,
                targetConnection,
                transaction,
                "Fretrack_ShipmentContainers_Staging",
                BuildContainerOnlyShipmentContainersSql(cargoContainersSchema),
                parameters =>
                {
                    parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = 18 });
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            stagingCommitted = true;
            await ExecuteStoredProcedureAsync(targetConnection, cargoId, 18, cancellationToken);

            return new CargoMigrationResponse
            {
                JobNo = normalizedJobNo,
                CargoId = cargoId,
                CargoExistsInCargoMint = false,
                ActualMigrationCompleted = true,
                Status = "success",
                Message = "Container staging test completed successfully and actual tables were updated.",
                StagingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Fretrack_ShipmentContainers_Staging"] = containerCount
                }
            };
        }
        catch
        {
            if (!stagingCommitted)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    private async Task DeleteExistingStagingRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int cargoId,
        CancellationToken cancellationToken)
    {
        var deleteStatements = new[]
        {
            "DELETE FROM Fretrack_Cargo_Staging WHERE CargoID = @CargoID",
            "DELETE FROM Fretrack_ShipmentService_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM Fretrack_ShipmentPackages_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM Fretrack_ShipmentContainers_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM Fretrack_Shipment_Routing_Staging_New WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_CargoEntities_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_Invoices_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_InvoiceEsync_Staging WHERE CargoID = @CargoID",
            "DELETE FROM Fretrack_ShipmentCharges_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM Fretrack_InvoiceLineItems_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_VendorBillLineItems_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_VendorBill_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_HBL_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM Fretrack_CargoDocuments_Staging WHERE CargoID = @CargoID"
        };

        foreach (var sql in deleteStatements)
        {
            var cleanupStopwatch = Stopwatch.StartNew();
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                sql,
                parameters =>
                {
                    parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                },
                cancellationToken);
            _logger.LogInformation(
                "Staging cleanup completed for {StagingTable} in {ElapsedMilliseconds} ms.",
                Regex.Match(sql, @"DELETE FROM\s+(\S+)", RegexOptions.IgnoreCase).Groups[1].Value,
                cleanupStopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<int> CopyQueryToStagingAsync(
        SqlConnection sourceConnection,
        SqlConnection targetConnection,
        SqlTransaction transaction,
        string destinationTableName,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? columnRenames = null,
        Func<DataTable, CancellationToken, Task>? beforeBulkCopyAsync = null)
    {
        var stagingStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Staging {DestinationTable} started.", destinationTableName);

        var sourceQueryStopwatch = Stopwatch.StartNew();
        var table = await LoadDataTableAsync(sourceConnection, sql, addParameters, cancellationToken);
        _logger.LogInformation(
            "Staging {DestinationTable} source query completed in {ElapsedMilliseconds} ms with {RowCount} rows.",
            destinationTableName,
            sourceQueryStopwatch.ElapsedMilliseconds,
            table.Rows.Count);

        if (table.Rows.Count == 0)
        {
            _logger.LogInformation(
                "Staging {DestinationTable} completed in {ElapsedMilliseconds} ms with 0 rows.",
                destinationTableName,
                stagingStopwatch.ElapsedMilliseconds);
            return 0;
        }

        var preparationStopwatch = Stopwatch.StartNew();
        if (columnRenames is not null && columnRenames.Count > 0)
        {
            ApplyColumnRenames(table, columnRenames);
        }

        if (beforeBulkCopyAsync is not null)
        {
            await beforeBulkCopyAsync(table, cancellationToken);
        }

        _logger.LogInformation(
            "Staging query for {DestinationTable} produced columns: {Columns}",
            destinationTableName,
            string.Join(", ", table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)));
        _logger.LogInformation(
            "Staging {DestinationTable} data preparation completed in {ElapsedMilliseconds} ms.",
            destinationTableName,
            preparationStopwatch.ElapsedMilliseconds);

        var targetMetadataStopwatch = Stopwatch.StartNew();
        var destinationSchema = await ResolveTableSchemaAsync(targetConnection, destinationTableName, cancellationToken, transaction);
        var destinationColumns = await LoadTableColumnsAsync(
            targetConnection,
            destinationSchema,
            destinationTableName,
            cancellationToken,
            transaction);
        var destinationColumnTypes = await LoadTableColumnTypesAsync(
            targetConnection,
            destinationSchema,
            destinationTableName,
            cancellationToken,
            transaction);
        var writableDestinationColumns = await LoadWritableTableColumnsAsync(
            targetConnection,
            destinationSchema,
            destinationTableName,
            cancellationToken,
            transaction);
        _logger.LogInformation(
            "Staging {DestinationTable} target metadata lookup completed in {ElapsedMilliseconds} ms.",
            destinationTableName,
            targetMetadataStopwatch.ElapsedMilliseconds);

        var normalizationStopwatch = Stopwatch.StartNew();
        NormalizeDataTableForBulkCopy(table, destinationColumnTypes);
        _logger.LogInformation(
            "Staging {DestinationTable} data normalization completed in {ElapsedMilliseconds} ms.",
            destinationTableName,
            normalizationStopwatch.ElapsedMilliseconds);

        using var bulkCopy = new SqlBulkCopy(targetConnection, SqlBulkCopyOptions.TableLock, transaction)
        {
            DestinationTableName = destinationTableName,
            BatchSize = table.Rows.Count,
            BulkCopyTimeout = _commandTimeoutSeconds
        };

        foreach (DataColumn column in table.Columns)
        {
            if (column.ColumnName.Contains(','))
            {
                _logger.LogWarning(
                    "Skipping invalid staging column '{ColumnName}' for destination table {DestinationTable}.",
                    column.ColumnName,
                    destinationTableName);
                continue;
            }

            if (!destinationColumns.Contains(column.ColumnName))
            {
                _logger.LogWarning(
                    "Skipping source column '{ColumnName}' because destination table {DestinationTable} does not contain it.",
                    column.ColumnName,
                    destinationTableName);
                continue;
            }

            if (!writableDestinationColumns.Contains(column.ColumnName))
            {
                _logger.LogWarning(
                    "Skipping read-only destination column '{ColumnName}' for table {DestinationTable}.",
                    column.ColumnName,
                    destinationTableName);
                continue;
            }

            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        var bulkCopyStopwatch = Stopwatch.StartNew();
        await bulkCopy.WriteToServerAsync(table, cancellationToken);
        _logger.LogInformation(
            "Staging {DestinationTable} bulk insert completed in {ElapsedMilliseconds} ms with {RowCount} rows. Total staging-table time: {TotalElapsedMilliseconds} ms.",
            destinationTableName,
            bulkCopyStopwatch.ElapsedMilliseconds,
            table.Rows.Count,
            stagingStopwatch.ElapsedMilliseconds);
        return table.Rows.Count;
    }

    private static void ApplyColumnRenames(DataTable table, IReadOnlyDictionary<string, string> columnRenames)
    {
        foreach (var rename in columnRenames)
        {
            if (!table.Columns.Contains(rename.Key))
            {
                continue;
            }

            table.Columns[rename.Key]!.ColumnName = rename.Value;
        }
    }

    private static void NormalizeDataTableForBulkCopy(
        DataTable table,
        IReadOnlyDictionary<string, string> destinationColumnTypes)
    {
        foreach (DataColumn column in table.Columns)
        {
            if (column.ReadOnly)
            {
                if (string.IsNullOrWhiteSpace(column.Expression))
                {
                    try
                    {
                        column.ReadOnly = false;
                    }
                    catch (ReadOnlyException)
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            if (!destinationColumnTypes.TryGetValue(column.ColumnName, out var destinationType))
            {
                continue;
            }

            foreach (DataRow row in table.Rows)
            {
                var value = row[column];
                if (value is DBNull or null)
                {
                    continue;
                }

                if (value is string text && string.IsNullOrWhiteSpace(text))
                {
                    row[column] = DBNull.Value;
                    continue;
                }

                if (value is string stringValue && TryConvertStringValue(stringValue, destinationType, out var convertedValue))
                {
                    row[column] = convertedValue ?? DBNull.Value;
                }
            }
        }
    }

    private static bool TryConvertStringValue(string value, string destinationType, out object? convertedValue)
    {
        convertedValue = null;
        var normalizedType = destinationType.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (normalizedType)
        {
            case "decimal":
            case "numeric":
            case "money":
            case "smallmoney":
            case "float":
            case "real":
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue) ||
                    decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out decimalValue))
                {
                    convertedValue = decimalValue;
                    return true;
                }

                return false;

            case "int":
            case "smallint":
            case "tinyint":
                if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue) ||
                    int.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out intValue))
                {
                    convertedValue = intValue;
                    return true;
                }

                return false;

            case "bigint":
                if (long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var longValue) ||
                    long.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out longValue))
                {
                    convertedValue = longValue;
                    return true;
                }

                return false;

            case "bit":
                if (bool.TryParse(value, out var boolValue))
                {
                    convertedValue = boolValue;
                    return true;
                }

                if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = true;
                    return true;
                }

                if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = false;
                    return true;
                }

                return false;

            case "date":
            case "datetime":
            case "datetime2":
            case "smalldatetime":
            case "datetimeoffset":
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeValue) ||
                    DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateTimeValue))
                {
                    convertedValue = dateTimeValue;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private async Task ExecuteStoredProcedureAsync(
        SqlConnection targetConnection,
        int cargoId,
        int orgId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand("dbo.usp_MigrateSingleCargoFromStaging", targetConnection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _commandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@FretrackCargoId", SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> CargoExistsInCargoMintAsync(
        SqlConnection targetConnection,
        string schema,
        int cargoId,
        string jobNo,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(BuildCargoMintCargoExistsSql(schema), targetConnection)
        {
            CommandTimeout = _commandTimeoutSeconds
        };
        command.Parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task<bool> SalesQuotationExistsInCargoMintAsync(
        SqlConnection targetConnection,
        SqlTransaction transaction,
        string schema,
        int quotationId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand($@"
SELECT COUNT(1)
FROM [{schema}].[SalesQuotations]
WHERE QuotationId = @QuotationId;", targetConnection, transaction);
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@QuotationId", SqlDbType.Int) { Value = quotationId });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task ClearInvalidUserReferencesAsync(
        DataTable table,
        SqlConnection targetConnection,
        SqlTransaction transaction,
        string? usersSchema,
        CancellationToken cancellationToken,
        params string[] columnNames)
    {
        if (string.IsNullOrWhiteSpace(usersSchema) || columnNames.Length == 0 || table.Rows.Count == 0)
        {
            return;
        }

        foreach (var columnName in columnNames)
        {
            if (!table.Columns.Contains(columnName))
            {
                continue;
            }

            foreach (DataRow row in table.Rows)
            {
                var value = row[columnName];
                if (value is DBNull or null)
                {
                    continue;
                }

                var userId = Convert.ToInt32(value);
                if (!await UserExistsInCargoMintAsync(targetConnection, transaction, usersSchema, userId, cancellationToken))
                {
                    row[columnName] = DBNull.Value;
                }
            }
        }
    }

    private async Task<bool> UserExistsInCargoMintAsync(
        SqlConnection targetConnection,
        SqlTransaction transaction,
        string schema,
        int userId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand($@"
SELECT COUNT(1)
FROM [{schema}].[Users]
WHERE Id = @UserId;", targetConnection, transaction);
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task<DataTable> LoadDataTableAsync(
        SqlConnection connection,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = _commandTimeoutSeconds
        };
        addParameters?.Invoke(command.Parameters);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = _commandTimeoutSeconds
        };
        addParameters?.Invoke(command.Parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<HashSet<string>> LoadTableColumnsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken,
        SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(@"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @TableName;", connection);
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schema });
        command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private async Task<Dictionary<string, string>> LoadTableColumnTypesAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken,
        SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(@"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @SchemaName
  AND TABLE_NAME = @TableName;", connection);
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schema });
        command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns[reader.GetString(0)] = reader.GetString(1);
        }

        return columns;
    }

    private async Task<HashSet<string>> LoadWritableTableColumnsAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        CancellationToken cancellationToken,
        SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(@"
SELECT c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE s.name = @SchemaName
  AND o.name = @TableName
  AND c.is_identity = 0
  AND c.is_computed = 0;", connection);
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schema });
        command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private async Task<string> ResolveTableSchemaAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken,
        SqlTransaction? transaction = null,
        string defaultSchema = "dbo")
    {
        const string schemaSql = @"
SELECT TOP 1 SchemaName
FROM (
    SELECT s.name AS SchemaName
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    WHERE o.name = @TableName
      AND o.type IN ('U', 'V')

    UNION ALL

    SELECT s.name AS SchemaName
    FROM sys.synonyms syn
    INNER JOIN sys.schemas s ON syn.schema_id = s.schema_id
    WHERE syn.name = @TableName
) AS Candidates
ORDER BY CASE WHEN SchemaName = 'dbo' THEN 0 ELSE 1 END, SchemaName;";

        using var command = new SqlCommand(schemaSql, connection);
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;
        command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = tableName });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var schema = Convert.ToString(result);

        if (string.IsNullOrWhiteSpace(schema))
        {
            if (!string.IsNullOrWhiteSpace(defaultSchema))
            {
                return defaultSchema;
            }

            throw new KeyNotFoundException($"Table '{tableName}' was not found in the source database.");
        }

        return schema;
    }

    private static string BuildCargoLookupSql(string schema)
    {
        return $@"
SELECT
    c.CargoID,
    c.JobNo
FROM [{schema}].[Cargo] c
    WHERE c.JobNo = @JobNo
      AND ISNULL(c.isDeleted, 0) = 0;";
    }

    private static string BuildContainerOnlyShipmentContainersSql(string schema)
    {
        return $@"
SELECT
    @OrgId AS OrgId,
    p.CargoID AS FretrackCargoId,
    p.ContainerID AS FretrackContainerID,
    p.ContainerTypeID AS ContainerTypeId,
    p.ContainerNumber AS ContainerNo,
    p.ContainerCode,
    p.Description,
    p.Seal1,
    p.Seal2,
    p.VGMWeight AS VgmWeight,
    p.CreatedBy,
    p.DateCreated AS CreatedDate,
    p.ModifiedBy AS UpdatedBy,
    p.DateModified AS UpdatedDate,
    p.DeletedBy,
    p.DateDeleted AS DeletedDate,
    p.isDeleted AS IsDeleted,
    p.ModifiedBy,
    p.DateModified,
    CAST(NULL AS INT) AS CargoMintCreatedby,
    CAST(NULL AS INT) AS CargoMintUpdatedBy,
    CAST(NULL AS INT) AS CargoMintDeletedBy,
    CAST(NULL AS INT) AS CargoMintContainerTypeId,
    CAST(NULL AS INT) AS CargomintShipmentId,
    CAST(NULL AS INT) AS CargoMintShipmentServiceId,
    CAST(1 AS BIT) AS IsSelectedForMigration,
    CAST(NULL AS NVARCHAR(500)) AS MigrationRemarks,
    GETDATE() AS ImportedOn,
    CAST(NULL AS INT) AS CargoMintModifiedBy
FROM [{schema}].[CargoContainers] p
WHERE p.CargoID = @CargoID
  AND ISNULL(p.isDeleted, 0) = 0;";
    }

    private static string BuildCargoHeaderSql(string schema)
    {
        return $@"
SELECT
    c.CargoID,
    c.CargoNumber,
    c.JobNo,
    c.MasterNo,
    c.HouseNo,
    c.ModeOfTransport,
    c.TransportDirection,
    c.isConsolidation,
    c.IncoTermID,
    c.TypeOfMoveID,
    c.PickupAddressID,
    c.DeliveryAddressID,
    c.OpportunityID,
    cd.HandledBy As HandledById,
    CAST(NULL AS INT) AS CargoMintCompanyId,
    CAST(NULL AS INT) AS CargoMintSalesPersonId,
    cd.NominationType,
    cd.AgentID AS OverseasAgentId,
    CAST(NULL AS INT) AS CargoMintHandledById,
    cd.JobDate AS ShipmentDate,
    c.CustomerReference,
    c.POLID,
    c.POL,
    c.PODID,
    c.POD,
    c.ETD,
    c.ETA,
    c.ShipperID,
    c.ShipperAddressID,
    c.Shipper,
    c.ConsigneeID,
    c.ConsigneeAddressID,
    c.Consignee,
    c.NotifyParty1ID,
    c.NotifyParty1AddressID,
    c.NotifyParty1,
    c.NotifyParty2ID,
    c.NotifyParty2AddressID,
    c.ForwarderID,
    c.ForwardedAddressID,
    c.OriginAgentID,
    c.OriginAgentAddressID,
    c.DestinationAgentID,
    c.DestinationAgentAddressID,
    c.Notes,
    c.CreatedBy,
    c.DateCreated,
    c.ModifiedBy,
    c.DateModified,
    c.DeletedBy,
    c.DateDeleted,
    c.isDeleted,
    c.CustomerID,
    c.CargoApprovalStatus,
    c.CargoSOPApprovalStatus,
    c.OfficeID,
    c.FreightStatus AS ShipmentStatus,
    c.PaymentTerms,
    c.InvoicingParty,
    c.JobType,
    c.isHBLNOautogenerate,
    CAST(NULL AS INT) AS CargoMintQuotationId,
    c.isLocked,
    c.LockedBy,
    c.LockedDate,
    c.HblTerm,
    c.MblTerm,
    c.HblStatus,
    c.MblStatus,
    c.FreeDays,
    c.POR,
    c.isGstJob,
    c.OfficeID AS BranchId,
    o.OpportunityOwnerID AS SalesPersonId,
    CAST(NULL AS NVARCHAR(50)) AS PrimaryServiceId,
    CAST(1 AS BIT) AS IsSelectedForMigration,
    CAST(NULL AS NVARCHAR(500)) AS MigrationRemarks,
    GETDATE() AS ImportedOn,
    @OrgId AS OrgId,
    CAST(NULL AS INT) AS CargoMintCreatedById,
    CAST(NULL AS INT) AS CargoMintDeletedById,
    CAST(NULL AS INT) AS CargoMintModifiedById
FROM [{schema}].[Cargo] c
LEFT JOIN [{schema}].[Opportunities] o
    ON c.OpportunityID = o.OpportunityID
LEFT JOIN [{schema}].[CargoDetails] cd
    ON c.CargoID = cd.CargoID
    WHERE c.JobNo = @JobNo
      AND ISNULL(c.isDeleted, 0) = 0;";
    }

    private static string QualifySourceTables(string schema, string sql)
    {
        var qualifiedSql = sql;

        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+Cargo\b", $"FROM [{schema}].[Cargo]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoPackages\b", $"FROM [{schema}].[CargoPackages]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoContainers\b", $"FROM [{schema}].[CargoContainers]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoEntities\b", $"FROM [{schema}].[CargoEntities]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+Invoices\b", $"FROM [{schema}].[Invoices]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+InvoiceESync\b", $"FROM [{schema}].[InvoiceESync]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoCharges\b", $"FROM [{schema}].[CargoCharges]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+InvoiceLineItems\b", $"FROM [{schema}].[InvoiceLineItems]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+VendorBillLineItems\b", $"FROM [{schema}].[VendorBillLineItems]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+VendorBill\b", $"FROM [{schema}].[VendorBill]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoHBL\b", $"FROM [{schema}].[CargoHBL]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bFROM\s+CargoDocuments\b", $"FROM [{schema}].[CargoDocuments]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bLEFT JOIN\s+AirShipmentRouting\b", $"LEFT JOIN [{schema}].[AirShipmentRouting]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bLEFT JOIN\s+OceanShipmentRouting\b", $"LEFT JOIN [{schema}].[OceanShipmentRouting]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bINNER JOIN\s+OceanShipmentRouting\b", $"INNER JOIN [{schema}].[OceanShipmentRouting]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bINNER JOIN\s+AirShipmentRouting\b", $"INNER JOIN [{schema}].[AirShipmentRouting]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bINNER JOIN\s+JobTypeMaster\b", $"INNER JOIN [{schema}].[JobTypeMaster]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bINNER JOIN\s+Invoices\b", $"INNER JOIN [{schema}].[Invoices]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bINNER JOIN\s+VendorBill\b", $"INNER JOIN [{schema}].[VendorBill]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bLEFT JOIN\s+CargoDetails\b", $"LEFT JOIN [{schema}].[CargoDetails]");
        qualifiedSql = Regex.Replace(qualifiedSql, @"\bLEFT JOIN\s+Opportunities\b", $"LEFT JOIN [{schema}].[Opportunities]");

        return qualifiedSql;
    }

    private static string BuildCargoMintCargoExistsSql(string schema)
    {
        return $@"
SELECT COUNT(1)
FROM [{schema}].[Cargo]
WHERE CargoID = @CargoID
   OR JobNo = @JobNo;";
    }

private const string ShipmentServiceSql = @"
SELECT
    @OrgId AS OrgId,
    CAST(1 AS BIT) AS IsPrimary,
    CAST(NULL AS INT) AS LegTypeId,
    CAST(1 AS INT) AS ServiceSequence,
    c.CargoID AS FretrackCargoId,
    c.ModeOfTransport AS TransportMode,
    c.TransportDirection,
    c.isConsolidation AS IsConsolidation,
    c.POLID AS PolId,
    c.PODID AS PodId,
    c.ETD,
    c.ETA,
    c.Notes,
    c.JobType,
    jt.JobTypeName,
    CASE
        WHEN UPPER(LTRIM(RTRIM(c.ModeOfTransport))) = 'AIR' THEN ar.AirCarrierID
        WHEN UPPER(LTRIM(RTRIM(c.ModeOfTransport))) IN ('OCEAN', 'SURFACE') THEN orr.OceanCarrierID
        ELSE NULL
    END AS CarrierId,
    c.DeletedBy AS DeletedBy,
    c.DateDeleted,
    c.isDeleted As IsDeleted
FROM Cargo c
LEFT JOIN JobTypeMaster jt
    ON c.JobType = jt.JobTypeID
LEFT JOIN AirShipmentRouting ar
    ON c.CargoID = ar.ShipmentID
LEFT JOIN OceanShipmentRouting orr
    ON c.CargoID = orr.ShipmentID
    WHERE c.CargoID = @CargoID
      AND ISNULL(c.isDeleted, 0) = 0;";

private const string ShipmentPackagesSql = @"
SELECT
    @OrgId AS OrgId,
    p.CargoPackName,
    p.PackageCount,
    p.Length,
    p.Width,
    p.Height,
    p.SizeID,
    p.NetWeight,
    p.GrossWeight,
    p.WeightUnitID,
    p.Volume,
    p.VolumeUnitID,
    p.PackageDescription,
    p.MarksAndNumbers,
    p.InvoiceNumber,
    p.InvoiceDate,
    p.SBNo,
    p.SBDate,
    p.isPerPackage,
    p.WeightKGS,
    p.WeightLBS,
    p.VolumeCBM,
    p.VolumeFT3,
    p.VolumeWeight,
    p.TotalNetWeight,
    p.TotalGrossWeight,
    p.TotalVolume,
    p.TotalVolumeWeight,
    p.ParentPackageID,
    p.CreatedBy,
    p.DateCreated,
    p.ModifiedBy,
    p.DateModified,
    p.DeletedBy,
    p.DateDeleted,
    p.isDeleted,
    p.CargoPackTypeID AS FretrackPackTypeID,
    p.CargoID AS FretrackCargoId,
    p.CargoPackID AS FretrackPackId
FROM CargoPackages p
    WHERE p.CargoID = @CargoID
      AND ISNULL(p.isDeleted, 0) = 0;";

private const string ShipmentContainersSql = @"
SELECT
    @OrgId AS OrgId,
    p.CargoID AS FretrackCargoId,
    p.ContainerID AS FretrackContainerID,
    p.ContainerTypeID AS ContainerTypeId,
    p.ContainerNumber AS ContainerNo,
    p.ContainerCode,
    p.Description,
    p.Seal1,
    p.Seal2,
    p.VGMWeight AS VgmWeight,
    p.CreatedBy,
    p.DateCreated AS CreatedDate,
    p.ModifiedBy AS UpdatedBy,
    p.DateModified AS UpdatedDate,
    p.DeletedBy,
    p.DateDeleted,
    p.isDeleted AS IsDeleted,
    p.ModifiedBy,
    p.DateModified
FROM CargoContainers p
    WHERE p.CargoID = @CargoID
      AND ISNULL(p.isDeleted, 0) = 0;";

private const string ShipmentRoutingSql = @"
SELECT
    @OrgId AS OrgId,
    c.CargoID AS FretrackCargoID,
    CAST('OCEAN' AS NVARCHAR(20)) AS RoutingMode,
    osr.OceanRoutingID AS FretrackRoutingID,
    osr.LegNumber AS RoutingOrder,
    osr.OceanCarrierID AS CarrierID,
    osr.OceanCarrierName AS CarrierName,
    osr.VoyageNumber AS FlightOrVoyageNo,
    osr.VesselName AS VesselName,
    osr.POLID AS POLID,
    osr.POL AS POL,
    osr.PODID AS PODID,
    osr.POD AS POD,
    osr.ETD AS ETD,
    osr.ATD AS ATD,
    osr.ETA AS ETA,
    osr.ATA AS ATA,
    osr.BookingNumber AS BookingNo,
    osr.BookingDate AS BookingDate,
    c.JobType AS JobType,
    j.JOBTypeName AS JobTypeName,
    c.ModeOfTransport AS ModeOfTransport,
    c.TransportDirection AS TransportDirection,
    osr.POT1ID AS POT1ID,
    osr.POT1 AS POT1,
    osr.POT2ID AS POT2ID,
    osr.POT2 AS POT2,
    osr.IsDeleted AS IsDeleted
FROM Cargo c
INNER JOIN OceanShipmentRouting osr
    ON c.CargoID = osr.ShipmentID
INNER JOIN JobTypeMaster j
    ON c.JobType = j.JOBTypeID
    WHERE c.CargoID = @CargoID
    AND j.JOBTypeName <> 'Courier'

      AND ISNULL(c.isDeleted, 0) = 0
      AND ISNULL(osr.IsDeleted, 0) = 0
UNION ALL
SELECT
    @OrgId AS OrgId,
    c.CargoID AS FretrackCargoID,
    CAST('AIR' AS NVARCHAR(20)) AS RoutingMode,
    asr.AirRoutingID AS FretrackRoutingID,
    asr.RoutingOrder AS RoutingOrder,
    asr.AirCarrierID AS CarrierID,
    asr.AirCarrierName AS CarrierName,
    asr.FlightNumber AS FlightOrVoyageNo,
    CAST(NULL AS NVARCHAR(200)) AS VesselName,
    asr.POLID AS POLID,
    asr.POL AS POL,
    asr.PODID AS PODID,
    asr.POD AS POD,
    asr.ETD AS ETD,
    asr.ATD AS ATD,
    asr.ETA AS ETA,
    asr.ATA AS ATA,
    asr.BookingNumber AS BookingNo,
    asr.BookingDate AS BookingDate,
    c.JobType AS JobType,
    j.JOBTypeName AS JobTypeName,
    c.ModeOfTransport AS ModeOfTransport,
    c.TransportDirection AS TransportDirection,
    asr.POT1ID AS POT1ID,
    asr.POT1 AS POT1,
    asr.POT2ID AS POT2ID,
    asr.POT2 AS POT2,
    asr.IsDeleted AS IsDeleted
FROM Cargo c
INNER JOIN AirShipmentRouting asr
    ON c.CargoID = asr.ShipmentID
INNER JOIN JobTypeMaster j
    ON c.JobType = j.JOBTypeID
    WHERE c.CargoID = @CargoID
     AND j.JOBTypeName NOT IN (
        'Custom Clearance',
        'Transportation',
        'Documentation',
        'Warehousing'
      )
      AND ISNULL(c.isDeleted, 0) = 0
      AND ISNULL(asr.IsDeleted, 0) = 0;";

private const string CargoEntitiesSql = @"
SELECT
    @OrgId AS OrgId,
    ce.CargoEntityID AS FretrackCargoEntityID,
    ce.CargoID AS FretrackCargoID,
    ce.CompanyID AS FretrackCompanyId,
    ce.EntityTypeID,
    ce.EntityAddressID AS FretrackEntityAddressID,
    ce.EntityName AS EntityRole,
    ce.EntityCountryID,
    ce.EntityCountry,
    ce.EntityStateID,
    ce.EntityState,
    ce.EntityCityID,
    ce.EntityCity,
    ce.EntityAddressLine1,
    ce.EntityZipCode,
    ce.EntityDisplayText,
    CAST(NULL AS NVARCHAR(500)) AS EntityDisplayText1,
    CAST(NULL AS NVARCHAR(500)) AS EntityDisplayText2,
    ce.CreatedBy,
    ce.DateCreated,
    ce.ModifiedBy,
    ce.DateModified,
    ce.DeletedBy,
    ce.DeletedDate,
    ce.isPayor,
    ce.isDeleted,
    ce.CreatedBy AS CargoMintCreatedBy,
    ce.ModifiedBy AS CargoMintModifiedBy,
    ce.DeletedBy AS CargoMintDeletedBy,
    CAST(NULL AS INT) AS CargoMintShipmentID,
    CAST(NULL AS INT) AS CargoMintEntityAddressID,
    CAST(NULL AS INT) AS CargoMintCompanyId,
    CAST(1 AS BIT) AS IsSelectedForMigration,
    CAST(NULL AS NVARCHAR(500)) AS MigrationRemarks,
    GETDATE() AS ImportedOn,
    CAST(NULL AS INT) AS CargoMintEntityTypeID,
    CAST(NULL AS INT) AS CargoMintStateID,
    CAST(NULL AS INT) AS CargoMintCountryID
FROM CargoEntities ce
    WHERE ce.CargoID = @CargoID
      AND ISNULL(ce.isDeleted, 0) = 0;";

private const string InvoicesSql = @"
SELECT
    @OrgId AS OrgId,
    i.InvoiceID AS FretrackInvoiceID,
    i.CargoID AS FretrackCargoID,
    i.InvoiceNumber,
    i.InvoiceDate,
    i.InvoiceType,
    i.PayingPartyID,
    i.PayingParty,
    CAST(NULL AS INT) AS PayingPartyAddressID,
    i.PayingPartyAddress,
    i.CurrencyID,
    i.CurrencyCode,
    CAST(NULL AS INT) AS ExchangeRateID,
    i.ExchangeRate,
    i.JobNumber,
    i.VesselVoyage,
    i.Cycle,
    i.CargoType,
    i.FreightStatus,
    i.MBL_MAWB,
    i.POL,
    i.FinalDestination,
    i.CreditDays,
    i.ShipperInvoiceDetails,
    i.HBL_HAWB,
    i.FlightDetails,
    i.Notes1,
    i.InvoiceAmount,
    i.TaxAmount,
    i.InvoiceAmountWords,
    CAST(NULL AS DECIMAL(18, 2)) AS InvoiceAmountLocalCurrency,
    CAST(NULL AS DECIMAL(18, 2)) AS TaxAmountLocalCurrency,
    CAST(NULL AS DECIMAL(18, 2)) AS ServiceTax,
    CAST(NULL AS DECIMAL(18, 2)) AS EducationCess,
    CAST(NULL AS DECIMAL(18, 2)) AS SHECess,
    CAST(NULL AS DECIMAL(18, 2)) AS NonTaxableAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS TaxableAmount,
    CAST(NULL AS INT) AS LocalCurrencyID,
    CAST(0 AS BIT) AS isLocked,
    CAST(NULL AS INT) AS LockedBy,
    CAST(NULL AS DATETIME) AS DateLocked,
    CAST(0 AS BIT) AS isSentToParty,
    CAST(NULL AS INT) AS SentBy,
    CAST(NULL AS DATETIME) AS SentDate,
    i.CreatedBy,
    i.DateCreated,
    i.ModifiedBy,
    i.DateModified,
    i.DeletedBy,
    i.DateDeleted,
    i.isDeleted,
    i.InvoiceApprovalStatus,
    CAST(NULL AS INT) AS CostSheetID,
    i.InvoiceTypeGST AS invoiceTypeGst
FROM Invoices i
    WHERE i.CargoID = @CargoID
      AND ISNULL(i.isDeleted, 0) = 0;";

private const string InvoiceESyncSql = @"
SELECT
    @OrgId AS OrgId,
    ies.SyncID AS FretrackSyncID,
    ies.InvoiceID AS FretrackInvoiceID,
    ies.InvoiceNumber AS FretrackInvoiceNumber,
    ies.AckNo AS FretrackAckNo,
    ies.AckDt AS FretrackAckDt,
    ies.IrnNo AS FretrackIrnNo,
    ies.SignedInvoice AS FretrackSignedInvoice,
    ies.SignedQRCode AS FretrackSignedQRCode,
    ies.SyncResponse AS FretrackSyncResponse,
    ies.SyncedBy AS FretrackSyncedBy,
    ies.SyncDate AS FretrackSyncDate,
    c.CargoID AS CargoID,
    CAST(1 AS BIT) AS IsSelectedForMigration,
    CAST(NULL AS NVARCHAR(500)) AS MigrationRemarks,
    GETDATE() AS ImportedOn
FROM InvoiceESync ies
INNER JOIN Invoices i
    ON i.InvoiceID = ies.InvoiceID
INNER JOIN Cargo c
    ON c.CargoID = i.CargoID
WHERE i.CargoID = @CargoID
  AND ISNULL(i.IsDeleted, 0) = 0
  AND ISNULL(c.IsDeleted, 0) = 0
  AND (
      c.JobNo LIKE '%FL26%'
      OR c.JobNo LIKE '%AE26%'
      OR c.JobNo LIKE '%SE26%'
      OR c.JobNo LIKE '%AI26%'
      OR c.JobNo LIKE '%SI26%'
  );";

private const string ShipmentChargesSql = @"
SELECT
    @OrgId AS OrgId,
    FretrackCargoId = cc.CargoID,
    FretrackIncomeChargeId =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.ChargeID END),
    FretrackExpenseChargeId =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.ChargeID END),
    FretrackChargeItemId = cc.ChargeItemID,
    FretrackChargeDescription = MAX(cc.ChargeDescription),
    FretrackApplyPer = MAX(cc.ApplyPer),
    FretrackSellCurrencyId =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.CurrencyID END),
    FretrackSellCurrencyCode =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.CurrencyCode END),
    FretrackSellExRate =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.ExRate END),
    FretrackSellRate =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.Rate END),
    FretrackSellQuantity =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.Quantity END),
    FretrackSellSubtotal =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TotalNonTaxableAmount END),
    FretrackSellTaxRateId =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TaxRateID END),
    FretrackSellTaxPercent =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TaxPercent END),
    FretrackSellTaxAmount =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TaxAmount END),
    FretrackSellTotalAmount =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME'
            THEN COALESCE(cc.TotalNonTaxableAmount, 0) + COALESCE(cc.TaxAmount, 0)
        END),
    FretrackInvoiceToId =
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.PayingPartyID END),
    FretrackBuyCurrencyId =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.CurrencyID END),
    FretrackBuyCurrencyCode =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.CurrencyCode END),
    FretrackBuyExRate =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.ExRate END),
    FretrackBuyRate =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.Rate END),
    FretrackBuyQuantity =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.Quantity END),
    FretrackBuySubtotal =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TotalNonTaxableAmount END),
    FretrackBuyTaxRateId =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TaxRateID END),
    FretrackBuyTaxPercent =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TaxPercent END),
    FretrackBuyTaxAmount =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TaxAmount END),
    FretrackBuyTotalAmount =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE'
            THEN COALESCE(cc.TotalNonTaxableAmount, 0) + COALESCE(cc.TaxAmount, 0)
        END),
    FretrackBillToId =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.PayingPartyID END),
    FretrackLineNumber = MIN(cc.LineNumber),
    FretrackCreatedBy = MAX(cc.CreatedBy),
    FretrackCreatedDate = MIN(cc.DateCreated),
    FretrackUpdatedBy = MAX(cc.ModifiedBy),
    FretrackUpdatedDate = MAX(cc.DateModified)
FROM CargoCharges cc
WHERE cc.CargoID = @CargoID and cc.Isdeleted=0
GROUP BY cc.CargoID, cc.ChargeItemID;";

    private const string InvoiceLineItemsSql = @"
SELECT
    inv.InvoiceID AS FretrackInvoiceID,
    inv.CargoID AS FretrackCargoID,
    inv.InvoiceNumber AS FretrackInvoiceNumber,
    inv.InvoiceTypeGST AS FretrackInvoiceTypeGST,
    invLines.SortOrder,
    invLines.InvoiceLineItemID AS FretrackInvoiceLineItemID,
    invLines.ChargeItemID AS FretrackChargeItemID,
    invLines.ChargeID AS FretrackChargeID,
    invLines.ChargeDescription AS FretrackChargeDescription,
    invLines.Quantity AS FretrackQuantity,
    invLines.Rate AS FretrackRate,
    invLines.CurrencyCode AS FretrackCurrencyCode,
    invLines.ExRate AS FretrackExRate,
    invLines.TaxPercent AS FretrackTaxPercent,
    invLines.TaxAmount AS FretrackTaxAmount,
    invLines.TaxableAmount AS FretrackTaxableAmount,
    invLines.NonTaxableAmount AS FretrackNonTaxableAmount,
    invLines.ExpectedAmount AS FretrackExpectedAmount,
    invLines.CreatedBy AS FretrackCreatedBy,
    invLines.ServiceCode,
    invLines.ApplyPer AS FretrackApplyPer,
    CAST(NULL AS DECIMAL(18, 2)) AS TotalAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS TaxAmountLocal,
    CAST(NULL AS DECIMAL(18, 2)) AS TotalAmountLocal,
    CAST(0 AS BIT) AS IsImported,
    CAST(NULL AS INT) AS InvoiceCurrencyId,
    CAST(NULL AS DECIMAL(18, 2)) AS LineSubTotal,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTaxAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTotalAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTaxAmountLocal,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTotalAmountLocal,
    invLines.ExRate AS InvoiceExRate,
    invLines.ExRate AS LineItemExRateNew,
    @OrgId AS OrgId
FROM InvoiceLineItems invLines
INNER JOIN Invoices inv
    ON inv.InvoiceID = invLines.InvoiceID
    WHERE inv.CargoID = @CargoID
      AND ISNULL(invLines.isDeleted, 0) = 0
      AND ISNULL(inv.isDeleted, 0) = 0;";

    private const string VendorBillLineItemsSql = @"
SELECT
    bill.VendorBillID AS FretrackVendorBillID,
    bill.CargoID AS FretrackCargoID,
    bill.VendorBillNumber AS FretrackVendorBillNumber,
    bill.VendorBillTypeGst AS VendorBillTypeGST,
    invitmbill.SortOrder,
    invitmbill.VendorBillLineItemID AS FretrackVendorBillLineItemID,
    invitmbill.ChargeItemID AS FretrackChargeItemID,
    invitmbill.ChargeID AS FretrackChargeID,
    invitmbill.ChargeDescription AS FretrackChargeDescription,
    invitmbill.Quantity AS FretrackQuantity,
    invitmbill.Rate AS FretrackRate,
    invitmbill.CurrencyCode AS FretrackCurrencyCode,
    invitmbill.ExRate AS FretrackExRate,
    invitmbill.TaxPercent AS FretrackTaxPercent,
    invitmbill.TaxAmount AS FretrackTaxAmount,
    invitmbill.TaxableAmount AS FretrackTaxableAmount,
    invitmbill.NonTaxableAmount AS FretrackNonTaxableAmount,
    invitmbill.ExpectedAmount AS FretrackExpectedAmount,
    invitmbill.CreatedBy AS FretarckCreatedBy,
    invitmbill.ServiceCode,
    invitmbill.ApplyPer AS FretrackApplyPer,
    CAST(NULL AS DECIMAL(18, 2)) AS TotalAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS TaxAmountLocal,
    CAST(NULL AS DECIMAL(18, 2)) AS TotalAmountLocal,
    CAST(0 AS BIT) AS IsImported,
    CAST(NULL AS DECIMAL(18, 2)) AS LineSubTotal,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTaxAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTotalAmount,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTaxAmountLocal,
    CAST(NULL AS DECIMAL(18, 2)) AS LineTotalAmountLocal,
    invitmbill.ExRate AS BillExRate,
    invitmbill.ExRate AS LineItemExRateNew,
    invitmbill.CurrencyID AS BillCurrencyId,
    @OrgId AS OrgId
FROM VendorBillLineItems invitmbill
INNER JOIN VendorBill bill
    ON invitmbill.VendorBillID = bill.VendorBillID
    WHERE bill.CargoID = @CargoID
      AND ISNULL(invitmbill.isDeleted, 0) = 0
      AND ISNULL(bill.isDeleted, 0) = 0;";

    private const string VendorBillSql = @"
SELECT
    bill.VendorBillID AS FretrackVendorBillID,
    bill.CargoID AS FretrackCargoID,
    bill.VendorBillNumber,
    bill.VendorBillDate,
    bill.VendorBillType,
    bill.PayingPartyID AS FretrackPayingPartyID,
    bill.PayingParty,
    bill.PayingPartyAddressID AS FretrackPayingPartyAddressID,
    bill.PayingPartyAddress,
    bill.CurrencyID,
    bill.CurrencyCode,
    bill.ExchangeRateID,
    bill.ExchangeRate,
    bill.JobNumber,
    bill.VesselVoyage,
    bill.Cycle,
    bill.CargoType,
    bill.FreightStatus,
    bill.MBL_MAWB,
    bill.POL,
    bill.FinalDestination,
    bill.CreditDays,
    bill.ShipperVendorBillDetails,
    bill.HBL_HAWB,
    bill.FlightDetails,
    bill.Notes1,
    bill.VendorBillAmount,
    bill.TaxAmount,
    bill.VendorBillAmountWords,
    bill.VendorBillAmountLocalCurrency,
    bill.TaxAmountLocalCurrency,
    bill.ServiceTax,
    bill.EducationCess,
    bill.SHECess,
    bill.NonTaxableAmount,
    bill.TaxableAmount,
    bill.LocalCurrencyID,
    bill.isLocked,
    bill.LockedBy,
    bill.DateLocked,
    bill.isSentToParty,
    bill.SentBy,
    bill.SentDate,
    bill.CreatedBy,
    bill.DateCreated,
    bill.ModifiedBy,
    bill.DateModified,
    bill.DeletedBy,
    bill.DateDeleted,
    bill.isDeleted,
    bill.VendorBillApprovalStatus,
    bill.CostSheetID,
    bill.VendorBillTypeGst,
    bill.BillStatus,
    bill.ActualAmount,
    bill.Remarks,
    bill.CargoDocumentID,
    CAST(NULL AS NVARCHAR(50)) AS ZohoBillCurrency,
    CAST(NULL AS INT) AS ZohoBillCurrencyID,
    CAST(NULL AS INT) AS ZohoPaymentTermID,
    CAST(NULL AS INT) AS BranchId,
    CAST(NULL AS NVARCHAR(100)) AS SourceOfSupply,
    CAST(NULL AS NVARCHAR(100)) AS CompanyGSTIN,
    CAST(NULL AS NVARCHAR(100)) AS PlaceOfSupply,
    CAST(NULL AS NVARCHAR(100)) AS VendorBillGSTTreatment,
    CAST(NULL AS INT) AS PaymentTermId,
    @OrgId AS OrgId
FROM VendorBill bill
    WHERE ISNULL(bill.IsDeleted, 0) = 0
      AND bill.CargoID = @CargoID;";

private const string HblSql = @"
SELECT
    @OrgId AS OrgId,
    HBLID AS FretrackHBLID,
    CargoID AS FretrackCargoID,
    DocumentTypeID,
    DocumentType,
    MBLNumber,
    HBLNumber,
    DocumentNumber,
    ShipperAddressID,
    ShipperName,
    ShipperAddress,
    ConsigneeAddressID,
    ConsigneeName,
    ConsigneeAddress,
    NotifyAddressID,
    NotifyName,
    NotifyAddress,
    ExportReferences,
    ForwardingAgentAddressID,
    ForwardingAgent,
    ForwardingAgentAddress,
    PointOfOriginFTZ,
    DeliveryInstructions,
    PreCarriageBy,
    PlaceOfReceipt,
    VesselVoyage,
    PortOfLoading,
    PortOfDischarge,
    PlaceOfDelivery,
    LoadingTerminal,
    TypeOfMove,
    isContainerized,
    MarksandNumbers,
    NoOfPackages,
    DescriptionOfPackagesGoods,
    GrossWeight,
    CASE
        WHEN TRY_CONVERT(decimal(18,6), Measurement) IS NOT NULL
            THEN TRY_CONVERT(decimal(18,6), Measurement)
        ELSE TRY_CONVERT(
            decimal(18,6),
            NULLIF(
                LTRIM(RTRIM(
                    REPLACE(
                        REPLACE(
                            REPLACE(CAST(Measurement AS nvarchar(4000)), 'CBM:', ''),
                            'CBM',
                            ''
                        ),
                        ':',
                        ''
                    )
                )),
                ''
            )
        )
    END AS Measurement,
    DeclaredValue,
    HBLDate,
    HBLPlace,
    ByAgentForCarrier,
    FreightCharges,
    Description1,
    Description2,
    CreatedBy,
    DateCreated,
    ModifiedBy,
    DateModified,
    isDeleted
FROM CargoHBL
    WHERE CargoID = @CargoID
      AND ISNULL(isDeleted, 0) = 0;";

private const string CargoDocumentsSql = @"
SELECT
    @OrgId AS OrgId,
    c.CargoDocumentID AS FretrackCargoDocumentID,
    c.DocumentTypeID AS FretrackDocumentTypeID,
    c.DocumentType,
    c.DocumentName,
    c.DocumentFileType,
    c.Remarks,
    c.FTPLink,
    c.CreatedBy,
    c.DateCreated,
    c.ModifiedBy,
    c.DateModified,
    CAST(NULL AS INT) AS DeletedBy,
    CAST(NULL AS DATETIME) AS DateDeleted,
    c.isDeleted,
    c.CargoID
FROM CargoDocuments c
WHERE c.CargoID = @CargoID
  AND ISNULL(c.isDeleted, 0) = 0;";
}
