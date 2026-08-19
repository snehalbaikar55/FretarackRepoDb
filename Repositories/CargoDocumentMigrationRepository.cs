using System.Data;
using System.IO;
using System.Net;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public sealed class CargoDocumentMigrationRepository : ICargoDocumentMigrationRepository
{
    private readonly string _sourceFtpUserName;
    private readonly string _sourceFtpPassword;
    private readonly string _cargoMintConnectionString;
    private readonly string _destinationConnectionString;
    private readonly string _destinationContainerName;
    private readonly string? _documentViewBaseUrl;
    private readonly string? _destinationPublicBaseUrl;
    private readonly int _commandTimeoutSeconds;
    private readonly ILogger<CargoDocumentMigrationRepository> _logger;

    public CargoDocumentMigrationRepository(IConfiguration configuration, ILogger<CargoDocumentMigrationRepository> logger)
    {
        var migrationSection = configuration.GetSection("CargoDocumentMigration");

        _sourceFtpUserName = migrationSection["SourceFtpUserName"]
            ?? configuration["Ftp:UserName"]
            ?? configuration["Ftp:Username"]
            ?? string.Empty;
        _sourceFtpPassword = migrationSection["SourceFtpPassword"]
            ?? configuration["Ftp:Password"]
            ?? string.Empty;
        _cargoMintConnectionString = configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");
        _destinationConnectionString = migrationSection["DestinationConnectionString"]
            ?? throw new InvalidOperationException("Setting 'CargoDocumentMigration:DestinationConnectionString' is not configured.");
        _destinationContainerName = migrationSection["ContainerName"]
            ?? migrationSection["DestinationFolderName"]
            ?? "documents";
        _documentViewBaseUrl = migrationSection["DocumentViewBaseUrl"];
        _destinationPublicBaseUrl = migrationSection["DestinationPublicBaseUrl"];
        _commandTimeoutSeconds = migrationSection.GetValue<int?>("CommandTimeoutSeconds")
            ?? configuration.GetValue<int?>("Migration:CommandTimeoutSeconds")
            ?? 600;

        _logger = logger;
    }

    public Task<CargoDocumentMigrationResult> MigrateAsync(int fretrackCargoDocumentId, CancellationToken cancellationToken = default)
    {
        return MigrateCoreAsync(null, fretrackCargoDocumentId, 18, cancellationToken);
    }

    public Task<CargoDocumentMigrationResult> MigrateAsync(int cargoId, int fretrackCargoDocumentId, CancellationToken cancellationToken = default)
    {
        return MigrateCoreAsync(cargoId, fretrackCargoDocumentId, 18, cancellationToken);
    }

    public async Task<CargoDocumentMigrationResult> MigrateByCargoIdAsync(int cargoId, CancellationToken cancellationToken = default)
    {
        return await MigrateByCargoIdAsync(cargoId, 18, cancellationToken);
    }

    public async Task<CargoDocumentMigrationResult> MigrateByCargoIdAsync(int cargoId, int orgId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_cargoMintConnectionString);
        await connection.OpenAsync(cancellationToken);

        var stagingSchema = await ResolveTableSchemaAsync(connection, "Fretrack_CargoDocuments_Staging", cancellationToken);
        var cargoColumn = await ResolveExistingColumnNameAsync(
            connection,
            stagingSchema,
            "Fretrack_CargoDocuments_Staging",
            null,
            "CargoID",
            "FretrackCargoId",
            "FretrackCargoID");
        var docColumn = await ResolveExistingColumnNameAsync(
            connection,
            stagingSchema,
            "Fretrack_CargoDocuments_Staging",
            null,
            "FretrackCargoDocumentID",
            "FretrackDocumentID");

        var rows = await LoadDataTableAsync(
            connection,
            $@"
SELECT DISTINCT
    s.[{docColumn}] AS FretrackCargoDocumentId
FROM [{stagingSchema}].[Fretrack_CargoDocuments_Staging] s
WHERE s.[{cargoColumn}] = @CargoId
  AND s.[{docColumn}] IS NOT NULL
ORDER BY s.[{docColumn}];",
            parameters =>
            {
                parameters.Add(new SqlParameter("@CargoId", SqlDbType.Int) { Value = cargoId });
            },
            cancellationToken);

        var result = new CargoDocumentMigrationResult
        {
            TotalRows = rows.Rows.Count
        };

        foreach (DataRow row in rows.Rows)
        {
            var fretrackCargoDocumentId = Convert.ToInt32(row["FretrackCargoDocumentId"]);
            try
            {
                var rowResult = await MigrateCoreAsync(cargoId, fretrackCargoDocumentId, orgId, cancellationToken);
                ApplyResult(result, rowResult.Rows.Single());
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Cargo document migration failed for CargoID {CargoId} and FretrackCargoDocumentID {FretrackCargoDocumentId}.",
                    cargoId,
                    fretrackCargoDocumentId);

                result.FailedRows++;
                result.Rows.Add(new CargoDocumentMigrationRowResult
                {
                    CargoId = cargoId,
                    FretrackCargoDocumentId = fretrackCargoDocumentId,
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        return result;
    }

    public async Task<CargoDocumentMigrationResult> MigrateBulkAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_cargoMintConnectionString);
        await connection.OpenAsync(cancellationToken);

        var stagingSchema = await ResolveTableSchemaAsync(connection, "Fretrack_CargoDocuments_Staging", cancellationToken);
        var cargoColumn = await ResolveExistingColumnNameAsync(
            connection,
            stagingSchema,
            "Fretrack_CargoDocuments_Staging",
            null,
            "CargoID",
            "FretrackCargoId",
            "FretrackCargoID");

        var docIdColumn = await ResolveExistingColumnNameAsync(
            connection,
            stagingSchema,
            "Fretrack_CargoDocuments_Staging",
            null,
            "FretrackCargoDocumentID",
            "FretrackDocumentID");

        var rows = await LoadDataTableAsync(
            connection,
            $@"
SELECT DISTINCT
    s.[{cargoColumn}] AS CargoId,
    s.[{docIdColumn}] AS FretrackCargoDocumentId
FROM [{stagingSchema}].[Fretrack_CargoDocuments_Staging] s
WHERE s.[{docIdColumn}] IS NOT NULL
  AND s.[ImportedOn] IS NULL
  AND ISNULL(s.[IsSelectedForMigration], 0) = 0
  AND s.[MigrationRemarks] IS NULL
ORDER BY s.[{docIdColumn}];",
            null,
            cancellationToken);

        var result = new CargoDocumentMigrationResult
        {
            TotalRows = rows.Rows.Count
        };

        foreach (DataRow row in rows.Rows)
        {
            var cargoId = Convert.ToInt32(row["CargoId"]);
            var fretrackCargoDocumentId = Convert.ToInt32(row["FretrackCargoDocumentId"]);

            try
            {
                var rowResult = await MigrateCoreAsync(cargoId, fretrackCargoDocumentId, 18, cancellationToken);
                ApplyResult(result, rowResult.Rows.Single());
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Bulk cargo document migration failed for CargoID {CargoId} and FretrackCargoDocumentID {FretrackCargoDocumentId}.",
                    cargoId,
                    fretrackCargoDocumentId);

                result.FailedRows++;
                result.Rows.Add(new CargoDocumentMigrationRowResult
                {
                    CargoId = cargoId,
                    FretrackCargoDocumentId = fretrackCargoDocumentId,
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        return result;
    }

    private async Task<CargoDocumentMigrationResult> MigrateCoreAsync(
        int? cargoId,
        int fretrackCargoDocumentId,
        int orgId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_cargoMintConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var stagingSchema = await ResolveTableSchemaAsync(connection, "Fretrack_CargoDocuments_Staging", cancellationToken, transaction);
            var documentsSchema = await ResolveTableSchemaAsync(connection, "Documents", cancellationToken, transaction);
            string? usersSchema = null;
            try
            {
                usersSchema = await ResolveTableSchemaAsync(connection, "Users", cancellationToken, transaction);
            }
            catch (KeyNotFoundException)
            {
                usersSchema = null;
            }
            var stagingRow = await LoadStagingRowAsync(
                connection,
                transaction,
                stagingSchema,
                cargoId,
                fretrackCargoDocumentId,
                cancellationToken);

        if (stagingRow is null)
        {
            throw new KeyNotFoundException(BuildMissingRowMessage(cargoId, fretrackCargoDocumentId));
        }

        _logger.LogInformation(
            "Cargo document staging columns for CargoID {CargoId} and FretrackCargoDocumentID {FretrackCargoDocumentId}: {Columns}",
            cargoId,
            fretrackCargoDocumentId,
            string.Join(", ", stagingRow.Table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)));

        var rowCargoId = GetInt32(stagingRow, "CargoID", "FretrackCargoId", "FretrackCargoID")
            ?? cargoId
            ?? throw new InvalidOperationException("CargoID is required for cargo document migration.");

            if (cargoId.HasValue && rowCargoId != cargoId.Value)
            {
                throw new KeyNotFoundException(BuildMissingRowMessage(cargoId, fretrackCargoDocumentId));
            }

            var result = new CargoDocumentMigrationResult
            {
                TotalRows = 1
            };

            var rowResult = await ProcessRowAsync(
                connection,
                transaction,
                documentsSchema,
                usersSchema,
                stagingRow,
                rowCargoId,
                fretrackCargoDocumentId,
                orgId,
                cancellationToken);

            ApplyResult(result, rowResult);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Cargo document migration failed for CargoID {cargoId?.ToString() ?? "N/A"} and FretrackCargoDocumentID {fretrackCargoDocumentId}.",
                ex);
        }
    }

    private async Task<CargoDocumentMigrationRowResult> ProcessRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string documentsSchema,
        string? usersSchema,
        DataRow stagingRow,
        int cargoId,
        int fretrackCargoDocumentId,
        int orgId,
        CancellationToken cancellationToken)
    {
        var targetColumns = await LoadTableColumnsAsync(connection, documentsSchema, "Documents", cancellationToken, transaction);
        await SanitizeUserReferencesAsync(stagingRow, connection, transaction, usersSchema, cancellationToken);
        var docTypeId = await ResolveDocumentTypeIdAsync(connection, transaction, documentsSchema, stagingRow, cancellationToken);
        var docTitle = GetPreferredFileName(stagingRow, GetString(stagingRow, "DocumentFTPLink", "FTPLink", "DocFilePath", "DocumentLocalLink") ?? string.Empty);
        var docDescription = docTitle;
        var docFileType = Path.GetExtension(docDescription);
        var entityId = GetInt32(stagingRow, "CargoMintShipmentID", "CargoMintShipmentId");
        var existingDocumentId = await GetExistingDocumentIdAsync(
            connection,
            transaction,
            documentsSchema,
            cargoId,
            fretrackCargoDocumentId,
            cancellationToken);
        var uploadedAsset = await UploadDocumentToAzureAsync(stagingRow, cargoId, fretrackCargoDocumentId, cancellationToken);

        if (existingDocumentId.HasValue)
        {
            await UpdateExistingDocumentAsync(
                connection,
                transaction,
                documentsSchema,
                targetColumns,
                existingDocumentId.Value,
                cargoId,
                docTypeId,
                entityId,
                orgId,
                docFileType,
                docTitle,
                docDescription,
                uploadedAsset,
                cancellationToken);

            await MarkStagingRowAsync(
                connection,
                transaction,
                stagingRow,
                cargoId,
                fretrackCargoDocumentId,
                existingDocumentId.Value,
                "Already imported and Azure path synchronized",
                cancellationToken);

            await UpdateMatchingBillDocumentAsync(
                connection,
                transaction,
                documentsSchema,
                entityId,
                existingDocumentId.Value,
                docTypeId,
                cancellationToken);

            _logger.LogInformation(
                "Cargo document synchronized for CargoID {CargoId} and FretrackCargoDocumentID {FretrackCargoDocumentId}. DocId={DocumentId}, BlobUrl={BlobUrl}.",
                cargoId,
                fretrackCargoDocumentId,
                existingDocumentId.Value,
                uploadedAsset.ViewUrl);

            return new CargoDocumentMigrationRowResult
            {
                CargoId = cargoId,
                FretrackCargoDocumentId = fretrackCargoDocumentId,
                DocumentId = existingDocumentId,
                FilePath = uploadedAsset.ViewUrl,
                Status = "Migrated",
                Message = "Already imported and Azure path synchronized"
            };
        }

        var documentId = await InsertDocumentAsync(
            connection,
            transaction,
            documentsSchema,
            targetColumns,
            stagingRow,
            docTypeId,
            orgId,
            uploadedAsset,
            cancellationToken);

        await MarkStagingRowAsync(
            connection,
            transaction,
            stagingRow,
            cargoId,
            fretrackCargoDocumentId,
            documentId,
            "Imported successfully",
            cancellationToken);

        await UpdateMatchingBillDocumentAsync(
            connection,
            transaction,
            documentsSchema,
            entityId,
            documentId,
            docTypeId,
            cancellationToken);

        _logger.LogInformation(
            "Cargo document inserted for CargoID {CargoId} and FretrackCargoDocumentID {FretrackCargoDocumentId}. DocId={DocumentId}, BlobUrl={BlobUrl}.",
            cargoId,
            fretrackCargoDocumentId,
            documentId,
            uploadedAsset.ViewUrl);

        return new CargoDocumentMigrationRowResult
        {
            CargoId = cargoId,
            FretrackCargoDocumentId = fretrackCargoDocumentId,
            DocumentId = documentId,
            FilePath = uploadedAsset.ViewUrl,
            Status = "Migrated",
            Message = "Imported successfully and uploaded to Azure"
        };
    }

    private async Task<DataRow?> LoadStagingRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        int? cargoId,
        int fretrackCargoDocumentId,
        CancellationToken cancellationToken)
    {
        var cargoColumn = await ResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "CargoID",
            "FretrackCargoId",
            "FretrackCargoID");

        var documentColumn = await ResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "FretrackCargoDocumentID",
            "FretrackDocumentID");

        var sql = $@"
SELECT TOP 1 *
FROM [{schema}].[Fretrack_CargoDocuments_Staging]
WHERE [{documentColumn}] = @FretrackCargoDocumentId";

        if (cargoId.HasValue)
        {
            sql += $@"
  AND [{cargoColumn}] = @CargoId";
        }

        using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = _commandTimeoutSeconds
        };
        command.Parameters.Add(new SqlParameter("@FretrackCargoDocumentId", SqlDbType.Int) { Value = fretrackCargoDocumentId });
        if (cargoId.HasValue)
        {
            command.Parameters.Add(new SqlParameter("@CargoId", SqlDbType.Int) { Value = cargoId.Value });
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table.Rows.Count == 0 ? null : table.Rows[0];
    }

    private async Task<int?> GetExistingDocumentIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        int cargoId,
        int fretrackCargoDocumentId,
        CancellationToken cancellationToken)
    {
        var targetColumns = await LoadTableColumnsAsync(connection, schema, "Documents", cancellationToken, transaction);
        var idColumn = ResolveFirstColumn(targetColumns, "DocId", "DocumentID", "CargoDocumentID");
        var fretrackColumn = ResolveFirstColumn(targetColumns, "FretrackDocumentID", "FretrackCargoDocumentID");
        var cargoColumn = ResolveFirstColumn(targetColumns, "FretrackCargoId", "FretrackCargoID", "CargoID", "CargoId");

        if (idColumn is null || fretrackColumn is null || cargoColumn is null)
        {
            return null;
        }

        using var command = new SqlCommand($@"
SELECT TOP 1 [{idColumn}]
FROM [{schema}].[Documents]
WHERE [{fretrackColumn}] = @FretrackCargoDocumentId
  AND [{cargoColumn}] = @CargoId;", connection, transaction);
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@FretrackCargoDocumentId", SqlDbType.Int) { Value = fretrackCargoDocumentId });
        command.Parameters.Add(new SqlParameter("@CargoId", SqlDbType.Int) { Value = cargoId });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result);
    }

    private async Task<int> InsertDocumentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        ISet<string> targetColumns,
        DataRow stagingRow,
        int? docTypeId,
        int orgId,
        UploadedDocumentAsset uploadedAsset,
        CancellationToken cancellationToken)
    {
        var values = new List<(string Column, object? Value)>();
        var docTitle = uploadedAsset.FileName;
        var docDescription = uploadedAsset.FileName;
        var docFileType = Path.GetExtension(docDescription);
        var entityId = GetInt32(stagingRow, "CargoMintShipmentID", "CargoMintShipmentId");
        var createdBy = GetInt32(stagingRow, "CargoMintCreatedBy", "CreatedBy");
        var updatedBy = GetInt32(stagingRow, "CargoMintModifiedBy", "ModifiedBy");
        var deletedBy = GetInt32(stagingRow, "CargoMintDeletedBy", "DeletedBy");

        AddIfPresent(values, targetColumns, "FretrackDocumentID", GetInt32(stagingRow, "FretrackCargoDocumentID"));
        AddIfPresent(values, targetColumns, "FretrackCargoDocumentID", GetInt32(stagingRow, "FretrackCargoDocumentID"));
        AddIfPresentFirstMatch(values, targetColumns, GetInt32(stagingRow, "CargoID"), "FretrackCargoId", "FretrackCargoID", "CargoID", "CargoId");
        AddIfPresent(values, targetColumns, "OrgId", orgId);
        AddIfPresent(values, targetColumns, "DocTypeId", docTypeId);
        AddIfPresent(values, targetColumns, "DocDescription", docDescription);
        AddIfPresent(values, targetColumns, "DocFilePath", uploadedAsset.ViewUrl);
        AddIfPresent(values, targetColumns, "BlobName", uploadedAsset.BlobName);
        AddIfPresent(values, targetColumns, "ContainerName", GetString(stagingRow, "ContainerName", "DocumentFileType"));
        AddIfPresent(values, targetColumns, "DocTitle", docTitle);
        AddIfPresent(values, targetColumns, "CreatedDate", GetDateTime(stagingRow, "CargoMintDateCreated", "DateCreated"));
        AddIfPresent(values, targetColumns, "CreatedBy", createdBy);
        AddIfPresent(values, targetColumns, "DocumentName", GetString(stagingRow, "DocumentName"));
        AddIfPresent(values, targetColumns, "DocFileType", docFileType);
        AddIfPresent(values, targetColumns, "DocumentFileType", docFileType);
        AddIfPresent(values, targetColumns, "EntityType", "Shipment");
        AddIfPresent(values, targetColumns, "EntityId", entityId);
        AddIfPresent(values, targetColumns, "UpdatedDate", GetDateTime(stagingRow, "CargoMintDateModified", "DateModified"));
        AddIfPresent(values, targetColumns, "UpdatedBy", updatedBy);
        AddIfPresent(values, targetColumns, "DocumentLocalLink", uploadedAsset.ViewUrl);
        AddIfPresent(values, targetColumns, "DocumentFTPLink", uploadedAsset.ViewUrl);
        AddIfPresent(values, targetColumns, "DeletedDate", GetDateTime(stagingRow, "DateDeleted"));
        AddIfPresent(values, targetColumns, "DeletedBy", deletedBy);
        AddIfPresent(values, targetColumns, "IsDeleted", GetBool(stagingRow, "IsDeleted", "isDeleted"));

        if (values.Count == 0)
        {
            throw new InvalidOperationException("No supported columns were found on the Documents table.");
        }

        var identityColumn = ResolveFirstColumn(targetColumns, "DocId", "DocumentID", "CargoDocumentID");
        if (identityColumn is null)
        {
            throw new InvalidOperationException("Documents table does not expose a supported identity column.");
        }

        var columnList = string.Join(", ", values.Select(x => $"[{x.Column}]"));
        var parameterList = string.Join(", ", Enumerable.Range(0, values.Count).Select(index => $"@p{index}"));

        using var command = new SqlCommand($@"
INSERT INTO [{schema}].[Documents] ({columnList})
OUTPUT INSERTED.[{identityColumn}]
VALUES ({parameterList});", connection, transaction);

        for (var i = 0; i < values.Count; i++)
        {
            command.Parameters.Add(new SqlParameter($"@p{i}", values[i].Value ?? DBNull.Value));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<int?> ResolveDocumentTypeIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        DataRow stagingRow,
        CancellationToken cancellationToken)
    {
        var candidateId = GetInt32(stagingRow, "CargoMintDocumentTypeID", "CargoMintDocumentTypeId", "FretrackDocumentTypeID");
        if (candidateId.HasValue && await DocumentTypeExistsAsync(connection, transaction, schema, candidateId.Value, cancellationToken))
        {
            return candidateId;
        }

        var candidateName = GetString(stagingRow, "DocumentType", "Remarks");
        if (!string.IsNullOrWhiteSpace(candidateName))
        {
            return await ResolveDocumentTypeIdByNameAsync(connection, transaction, schema, candidateName, cancellationToken);
        }

        return null;
    }

    private async Task<bool> DocumentTypeExistsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        int docTypeId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand($@"
SELECT COUNT(1)
FROM [{schema}].[DocumentTypes]
WHERE DocTypeId = @DocTypeId;", connection, transaction);
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@DocTypeId", SqlDbType.Int) { Value = docTypeId });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task<int?> ResolveDocumentTypeIdByNameAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        string documentTypeName,
        CancellationToken cancellationToken)
    {
        var columns = await LoadTableColumnsAsync(connection, schema, "DocumentTypes", cancellationToken, transaction);
        var nameColumn = ResolveFirstColumn(columns, "DocumentType", "DocTypeName", "Name", "DocumentTypeName");
        if (nameColumn is null)
        {
            return null;
        }

        using var command = new SqlCommand($@"
SELECT TOP 1 DocTypeId
FROM [{schema}].[DocumentTypes]
WHERE [{nameColumn}] = @DocumentType;", connection, transaction);
        command.CommandTimeout = _commandTimeoutSeconds;

        command.Parameters.Add(new SqlParameter("@DocumentType", SqlDbType.NVarChar, 200) { Value = documentTypeName });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result);
    }

    private async Task UpdateExistingDocumentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string schema,
        ISet<string> targetColumns,
        int documentId,
        int cargoId,
        int? docTypeId,
        int? entityId,
        int orgId,
        string docFileType,
        string docTitle,
        string docDescription,
        UploadedDocumentAsset uploadedAsset,
        CancellationToken cancellationToken)
    {
        var idColumn = ResolveFirstColumn(targetColumns, "DocId", "DocumentID", "CargoDocumentID");
        if (idColumn is null)
        {
            return;
        }

        var setParts = new List<string>();
        if (targetColumns.Contains("DocFilePath"))
        {
            setParts.Add("[DocFilePath] = @DocFilePath");
        }

        if (targetColumns.Contains("DocumentFTPLink"))
        {
            setParts.Add("[DocumentFTPLink] = @DocumentFTPLink");
        }

        if (targetColumns.Contains("DocumentLocalLink"))
        {
            setParts.Add("[DocumentLocalLink] = @DocumentLocalLink");
        }

        if (targetColumns.Contains("BlobName"))
        {
            setParts.Add("[BlobName] = @BlobName");
        }

        if (targetColumns.Contains("ContainerName"))
        {
            setParts.Add("[ContainerName] = @ContainerName");
        }

        var cargoColumn = ResolveFirstColumn(targetColumns, "FretrackCargoId", "FretrackCargoID", "CargoID", "CargoId");
        if (cargoColumn is not null)
        {
            setParts.Add($"[{cargoColumn}] = @FretrackCargoId");
        }

        if (targetColumns.Contains("DocTypeId") && docTypeId.HasValue)
        {
            setParts.Add("[DocTypeId] = @DocTypeId");
        }

        if (targetColumns.Contains("OrgId"))
        {
            setParts.Add("[OrgId] = @OrgId");
        }

        if (targetColumns.Contains("DocDescription"))
        {
            setParts.Add("[DocDescription] = @DocDescription");
        }

        if (targetColumns.Contains("DocTitle"))
        {
            setParts.Add("[DocTitle] = @DocTitle");
        }

        if (targetColumns.Contains("DocFileType"))
        {
            setParts.Add("[DocFileType] = @DocFileType");
        }

        if (targetColumns.Contains("DocumentFileType"))
        {
            setParts.Add("[DocumentFileType] = @DocFileType");
        }

        if (targetColumns.Contains("EntityType"))
        {
            setParts.Add("[EntityType] = @EntityType");
        }

        if (targetColumns.Contains("EntityId") && entityId.HasValue)
        {
            setParts.Add("[EntityId] = @EntityId");
        }

        if (setParts.Count == 0)
        {
            return;
        }

        using var command = new SqlCommand($@"
UPDATE [{schema}].[Documents]
SET {string.Join(", ", setParts)}
WHERE [{idColumn}] = @DocumentId;", connection, transaction);

        command.Parameters.Add(new SqlParameter("@DocumentId", SqlDbType.Int) { Value = documentId });
        command.Parameters.Add(new SqlParameter("@DocFilePath", SqlDbType.NVarChar, -1) { Value = uploadedAsset.ViewUrl });
        command.Parameters.Add(new SqlParameter("@DocumentFTPLink", SqlDbType.NVarChar, -1) { Value = uploadedAsset.ViewUrl });
        command.Parameters.Add(new SqlParameter("@DocumentLocalLink", SqlDbType.NVarChar, -1) { Value = uploadedAsset.ViewUrl });
        command.Parameters.Add(new SqlParameter("@BlobName", SqlDbType.NVarChar, 400) { Value = uploadedAsset.BlobName });
        command.Parameters.Add(new SqlParameter("@ContainerName", SqlDbType.NVarChar, 200) { Value = _destinationContainerName });
        command.Parameters.Add(new SqlParameter("@FretrackCargoId", SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@DocTypeId", SqlDbType.Int) { Value = (object?)docTypeId ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@OrgId", SqlDbType.Int) { Value = orgId });
        command.Parameters.Add(new SqlParameter("@DocDescription", SqlDbType.NVarChar, -1) { Value = docDescription ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@DocTitle", SqlDbType.NVarChar, -1) { Value = docTitle ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@DocFileType", SqlDbType.NVarChar, 50) { Value = docFileType ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@EntityType", SqlDbType.NVarChar, 100) { Value = "Shipment" });
        command.Parameters.Add(new SqlParameter("@EntityId", SqlDbType.Int) { Value = (object?)entityId ?? DBNull.Value });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateMatchingBillDocumentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string documentsSchema,
        int? shipmentId,
        int documentId,
        int? docTypeId,
        CancellationToken cancellationToken)
    {
        if (!shipmentId.HasValue || docTypeId != 8)
        {
            return;
        }

        try
        {
            var billsSchema = await ResolveTableSchemaAsync(connection, "Bills", cancellationToken, transaction);
            var documentsColumns = await LoadTableColumnsAsync(connection, documentsSchema, "Documents", cancellationToken, transaction);
            var billsColumns = await LoadTableColumnsAsync(connection, billsSchema, "Bills", cancellationToken, transaction);

            var documentIdColumn = ResolveFirstColumn(documentsColumns, "DocId", "DocumentID", "CargoDocumentID");
            var documentPathColumn = ResolveFirstColumn(documentsColumns, "DocFilePath");
            var documentDescriptionColumn = ResolveFirstColumn(documentsColumns, "DocDescription");
            var documentTypeColumn = ResolveFirstColumn(documentsColumns, "DocTypeId");
            var documentEntityTypeColumn = ResolveFirstColumn(documentsColumns, "EntityType");
            var documentEntityIdColumn = ResolveFirstColumn(documentsColumns, "EntityId");

            var billDocumentIdColumn = ResolveFirstColumn(billsColumns, "DocumentId");
            var billDocumentPathColumn = ResolveFirstColumn(billsColumns, "DocumentPath");
            var billShipmentIdColumn = ResolveFirstColumn(billsColumns, "ShipmentId");
            var billInvoiceNumberColumn = ResolveFirstColumn(billsColumns, "InvoiceNumber");

            if (documentIdColumn is null
                || documentPathColumn is null
                || documentDescriptionColumn is null
                || documentTypeColumn is null
                || documentEntityTypeColumn is null
                || documentEntityIdColumn is null
                || billDocumentIdColumn is null
                || billDocumentPathColumn is null
                || billShipmentIdColumn is null
                || billInvoiceNumberColumn is null)
            {
                return;
            }

            using var command = new SqlCommand($@"
UPDATE b
SET b.[{billDocumentIdColumn}] = d.[{documentIdColumn}],
    b.[{billDocumentPathColumn}] = d.[{documentPathColumn}]
FROM [{billsSchema}].[Bills] b
INNER JOIN [{documentsSchema}].[Documents] d
    ON d.[{documentEntityIdColumn}] = b.[{billShipmentIdColumn}]
   AND d.[{documentEntityTypeColumn}] = 'Shipment'
   AND d.[{documentTypeColumn}] = @BillDocTypeId
   AND d.[{documentDescriptionColumn}] LIKE '%' + LTRIM(RTRIM(CAST(b.[{billInvoiceNumberColumn}] AS NVARCHAR(200)))) + '%'
WHERE b.[{billShipmentIdColumn}] = @ShipmentId
  AND NULLIF(LTRIM(RTRIM(CAST(b.[{billInvoiceNumberColumn}] AS NVARCHAR(200)))), '') IS NOT NULL
  AND d.[{documentIdColumn}] = @DocumentId;", connection, transaction)
            {
                CommandTimeout = _commandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@BillDocTypeId", SqlDbType.Int) { Value = 8 });
            command.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.Int) { Value = shipmentId.Value });
            command.Parameters.Add(new SqlParameter("@DocumentId", SqlDbType.Int) { Value = documentId });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Bills backfill skipped for ShipmentId {ShipmentId} and DocumentId {DocumentId} because the target schema could not be resolved.",
                shipmentId,
                documentId);
        }
    }

    private async Task<UploadedDocumentAsset> UploadDocumentToAzureAsync(
        DataRow stagingRow,
        int cargoId,
        int fretrackCargoDocumentId,
        CancellationToken cancellationToken)
    {
        var sourceUrl = GetString(stagingRow, "DocumentFTPLink", "FTPLink", "DocFilePath", "DocumentLocalLink");
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new InvalidOperationException($"No source file path was found for CargoID {cargoId} and FretrackCargoDocumentID {fretrackCargoDocumentId}.");
        }

        if (LooksLikeAzureBlobUrl(sourceUrl))
        {
            return new UploadedDocumentAsset(
                sourceUrl,
                GetString(stagingRow, "BlobName") ?? BuildBlobName(GetPreferredFileName(stagingRow, sourceUrl)),
                BuildDocumentViewUrl(GetString(stagingRow, "BlobName") ?? BuildBlobName(GetPreferredFileName(stagingRow, sourceUrl))),
                GetPreferredFileName(stagingRow, sourceUrl));
        }

        var fileName = GetPreferredFileName(stagingRow, sourceUrl);
        var blobName = BuildBlobName(fileName);
        var fileBytes = await DownloadFtpFileAsync(sourceUrl, cancellationToken);
        if (fileBytes.Length == 0)
        {
            throw new InvalidOperationException($"Unable to download document bytes for CargoID {cargoId} and FretrackCargoDocumentID {fretrackCargoDocumentId}.");
        }

        var blobServiceClient = new BlobServiceClient(_destinationConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_destinationContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);
        await using (var uploadStream = new MemoryStream(fileBytes))
        {
            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(fileName)
                }
            };

            await blobClient.UploadAsync(uploadStream, options, cancellationToken);
        }

        return new UploadedDocumentAsset(
            sourceUrl,
            blobName,
            BuildDocumentViewUrl(blobName),
            fileName);
    }

    private async Task<byte[]> DownloadFtpFileAsync(string ftpUrl, CancellationToken cancellationToken)
    {
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

                if (!string.IsNullOrWhiteSpace(_sourceFtpUserName))
                {
                    request.Credentials = new NetworkCredential(_sourceFtpUserName, _sourceFtpPassword);
                }

                using var response = (FtpWebResponse)await request.GetResponseAsync();
                using var responseStream = response.GetResponseStream();
                if (responseStream is null)
                {
                    continue;
                }

                using var ms = new MemoryStream();
                await responseStream.CopyToAsync(ms, cancellationToken);
                return ms.ToArray();
            }
            catch
            {
                // Try the next mode.
            }
        }

        return Array.Empty<byte>();
    }

    private string BuildDocumentViewUrl(string blobName)
    {
        if (!string.IsNullOrWhiteSpace(_documentViewBaseUrl))
        {
            return $"{_documentViewBaseUrl}{Uri.EscapeDataString(blobName)}";
        }

        if (!string.IsNullOrWhiteSpace(_destinationPublicBaseUrl))
        {
            var baseUrl = _destinationPublicBaseUrl.TrimEnd('/');
            var containerName = _destinationContainerName.Trim('/');

            if (baseUrl.EndsWith($"/{containerName}", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/{blobName}";
            }

            return $"{baseUrl}/{containerName}/{blobName}";
        }

        return blobName;
    }

    private static string BuildBlobName(string fileName)
    {
        var safeFileName = MakeSafeBlobSegment(fileName);
        return $"FretrackDocuments/{safeFileName}";
    }

    private static string GetPreferredFileName(DataRow stagingRow, string sourceUrl)
    {
        var explicitName = GetString(stagingRow, "DocumentName", "DocTitle", "BlobName");
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            var normalizedName = ExtractFileName(explicitName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = explicitName;
            }

            return EnsureFileExtension(normalizedName, sourceUrl);
        }

        var fromUrl = Path.GetFileName(new Uri(sourceUrl).AbsolutePath);
        if (!string.IsNullOrWhiteSpace(fromUrl))
        {
            return fromUrl;
        }

        return $"document-{Guid.NewGuid():N}.bin";
    }

    private static string EnsureFileExtension(string fileName, string sourceUrl)
    {
        if (Path.HasExtension(fileName))
        {
            return fileName;
        }

        var sourceExtension = Path.GetExtension(new Uri(sourceUrl).AbsolutePath);
        return string.IsNullOrWhiteSpace(sourceExtension) ? fileName : $"{fileName}{sourceExtension}";
    }

    private static string MakeSafeBlobSegment(string value)
    {
        return ExtractFileName(value).Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
    }

    private static string ExtractFileName(string value)
    {
        var normalized = value.Replace('\\', '/');
        var fileName = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(fileName) ? value : fileName;
    }

    private static bool LooksLikeAzureBlobUrl(string url)
    {
        return url.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
        };
    }

    private sealed record UploadedDocumentAsset(string SourceUrl, string BlobName, string ViewUrl, string FileName);

    private async Task SanitizeUserReferencesAsync(
        DataRow stagingRow,
        SqlConnection connection,
        SqlTransaction transaction,
        string? usersSchema,
        CancellationToken cancellationToken)
    {
        if (!stagingRow.Table.Columns.Contains("CreatedBy")
            && !stagingRow.Table.Columns.Contains("ModifiedBy")
            && !stagingRow.Table.Columns.Contains("DeletedBy")
            && !stagingRow.Table.Columns.Contains("CargoMintCreatedBy")
            && !stagingRow.Table.Columns.Contains("CargoMintModifiedBy")
            && !stagingRow.Table.Columns.Contains("CargoMintDeletedBy"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(usersSchema))
        {
            ClearColumnIfPresent(stagingRow, "CreatedBy", "CargoMintCreatedBy");
            ClearColumnIfPresent(stagingRow, "ModifiedBy", "CargoMintModifiedBy");
            ClearColumnIfPresent(stagingRow, "DeletedBy", "CargoMintDeletedBy");
            return;
        }

        await ClearInvalidUserReferenceAsync(stagingRow, connection, transaction, usersSchema, cancellationToken, "CreatedBy", "CargoMintCreatedBy");
        await ClearInvalidUserReferenceAsync(stagingRow, connection, transaction, usersSchema, cancellationToken, "ModifiedBy", "CargoMintModifiedBy");
        await ClearInvalidUserReferenceAsync(stagingRow, connection, transaction, usersSchema, cancellationToken, "DeletedBy", "CargoMintDeletedBy");
    }

    private static void ClearColumnIfPresent(DataRow row, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (row.Table.Columns.Contains(columnName))
            {
                row[columnName] = DBNull.Value;
            }
        }
    }

    private async Task ClearInvalidUserReferenceAsync(
        DataRow row,
        SqlConnection targetConnection,
        SqlTransaction transaction,
        string usersSchema,
        CancellationToken cancellationToken,
        params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                continue;
            }

            var value = row[columnName];
            if (value is null or DBNull)
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

        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private async Task MarkStagingRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DataRow stagingRow,
        int cargoId,
        int fretrackCargoDocumentId,
        int documentId,
        string remarks,
        CancellationToken cancellationToken)
    {
        var schema = await ResolveTableSchemaAsync(connection, "Fretrack_CargoDocuments_Staging", cancellationToken, transaction);
        var cargoColumn = await ResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "CargoID",
            "FretrackCargoId",
            "FretrackCargoID");
        var documentColumn = await ResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "FretrackCargoDocumentID",
            "FretrackDocumentID");
        var idColumn = await TryResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "CargoMintDocumentID");
        var importedOnColumn = await TryResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "ImportedOn");
        var selectedColumn = await TryResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "IsSelectedForMigration");
        var remarksColumn = await TryResolveExistingColumnNameAsync(
            connection,
            schema,
            "Fretrack_CargoDocuments_Staging",
            transaction,
            "MigrationRemarks");

        var setParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(idColumn))
        {
            setParts.Add($"[{idColumn}] = @DocumentId");
        }

        if (!string.IsNullOrWhiteSpace(importedOnColumn))
        {
            setParts.Add($"[{importedOnColumn}] = SYSUTCDATETIME()");
        }

        if (!string.IsNullOrWhiteSpace(selectedColumn))
        {
            setParts.Add($"[{selectedColumn}] = 1");
        }

        if (!string.IsNullOrWhiteSpace(remarksColumn))
        {
            setParts.Add($"[{remarksColumn}] = @Remarks");
        }

        if (setParts.Count == 0)
        {
            return;
        }

        using var command = new SqlCommand($@"
UPDATE [{schema}].[Fretrack_CargoDocuments_Staging]
SET {string.Join(", ", setParts)}
WHERE [{cargoColumn}] = @CargoId
  AND [{documentColumn}] = @FretrackCargoDocumentId
  AND ({(string.IsNullOrWhiteSpace(idColumn) ? "1 = 1" : $"[{idColumn}] IS NULL")});", connection, transaction);

        command.Parameters.Add(new SqlParameter("@CargoId", SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@FretrackCargoDocumentId", SqlDbType.Int) { Value = fretrackCargoDocumentId });
        command.Parameters.Add(new SqlParameter("@DocumentId", SqlDbType.Int) { Value = documentId });
        command.Parameters.Add(new SqlParameter("@Remarks", SqlDbType.NVarChar, 500) { Value = remarks });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ApplyResult(CargoDocumentMigrationResult result, CargoDocumentMigrationRowResult rowResult)
    {
        result.Rows.Add(rowResult);
        result.TotalRows = Math.Max(result.TotalRows, result.Rows.Count);

        if (string.Equals(rowResult.Status, "Migrated", StringComparison.OrdinalIgnoreCase))
        {
            result.MigratedRows++;
        }
        else if (string.Equals(rowResult.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
        {
            result.SkippedRows++;
        }
        else
        {
            result.FailedRows++;
        }
    }

    private static void AddIfPresent(List<(string Column, object? Value)> values, ISet<string> targetColumns, string columnName, object? value)
    {
        if (targetColumns.Contains(columnName))
        {
            values.Add((columnName, ToDbValue(value)));
        }
    }

    private static void AddIfPresentFirstMatch(
        List<(string Column, object? Value)> values,
        ISet<string> targetColumns,
        object? value,
        params string[] columnNames)
    {
        var columnName = ResolveFirstColumn(targetColumns, columnNames);
        if (columnName is not null)
        {
            values.Add((columnName, ToDbValue(value)));
        }
    }

    private static object? ToDbValue(object? value)
    {
        return value is null ? DBNull.Value : value;
    }

    private static int? GetInt32(DataRow row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null or DBNull)
        {
            return null;
        }

        return Convert.ToInt32(value);
    }

    private static long? GetInt64(DataRow row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null or DBNull)
        {
            return null;
        }

        return Convert.ToInt64(value);
    }

    private static bool? GetBool(DataRow row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null or DBNull)
        {
            return null;
        }

        return Convert.ToBoolean(value);
    }

    private static DateTime? GetDateTime(DataRow row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null or DBNull)
        {
            return null;
        }

        return Convert.ToDateTime(value);
    }

    private static string? GetString(DataRow row, params string[] names)
    {
        var value = GetValue(row, names);
        if (value is null or DBNull)
        {
            return null;
        }

        var text = Convert.ToString(value);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static object? GetValue(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name))
            {
                continue;
            }

            var value = row[name];
            if (value is not DBNull)
            {
                return value;
            }
        }

        return null;
    }

    private static string? ResolveFirstColumn(ISet<string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
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

    private async Task<string> ResolveExistingColumnNameAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        SqlTransaction? transaction,
        params string[] candidateNames)
    {
        var columnName = await TryResolveExistingColumnNameAsync(connection, schema, tableName, transaction, candidateNames);
        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new InvalidOperationException(
                $"None of the expected columns {string.Join(", ", candidateNames.Select(name => $"'{name}'"))} were found on {schema}.{tableName}.");
        }

        return columnName;
    }

    private async Task<string?> TryResolveExistingColumnNameAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        SqlTransaction? transaction,
        params string[] candidateNames)
    {
        var columns = await LoadTableColumnsAsync(connection, schema, tableName, CancellationToken.None, transaction);
        return ResolveFirstColumn(columns, candidateNames);
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

            throw new KeyNotFoundException($"Table '{tableName}' was not found in the target database.");
        }

        return schema;
    }

    private async Task<DataTable> LoadDataTableAsync(
        SqlConnection connection,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken,
        SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = _commandTimeoutSeconds
        };
        addParameters?.Invoke(command.Parameters);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static string BuildMissingRowMessage(int? cargoId, int fretrackCargoDocumentId)
    {
        return cargoId.HasValue
            ? $"Cargo document staging row {fretrackCargoDocumentId} for CargoID {cargoId.Value} was not found or has no matching shipment."
            : $"Cargo document staging row {fretrackCargoDocumentId} was not found or has no matching shipment.";
    }
}
