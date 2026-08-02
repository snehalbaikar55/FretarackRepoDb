using System.Data;
using Microsoft.Data.SqlClient;
using RepoDbApi.Contracts;

namespace RepoDbApi.Repositories;

public class CargoMigrationRepository : ICargoMigrationRepository
{
    private readonly string _fretrackConnectionString;
    private readonly string _cargoMintConnectionString;
    private readonly ILogger<CargoMigrationRepository> _logger;

    public CargoMigrationRepository(IConfiguration configuration, ILogger<CargoMigrationRepository> logger)
    {
        _fretrackConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        _cargoMintConnectionString = configuration.GetConnectionString("CargoMintDB")
            ?? throw new InvalidOperationException("Connection string 'CargoMintDB' is not configured.");

        _logger = logger;
    }

    public async Task<CargoMigrationResponse> MigrateSingleJobAsync(string jobNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobNo))
        {
            throw new ArgumentException("JobNo is required.", nameof(jobNo));
        }

        await using var sourceConnection = new SqlConnection(_fretrackConnectionString);
        await using var targetConnection = new SqlConnection(_cargoMintConnectionString);

        await sourceConnection.OpenAsync(cancellationToken);
        await targetConnection.OpenAsync(cancellationToken);

        var cargoHeader = await LoadDataTableAsync(
            sourceConnection,
            CargoHeaderSql,
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
        var cargoExistsInCargoMint = await CargoExistsInCargoMintAsync(targetConnection, cargoId, normalizedJobNo, cancellationToken);

        using var transaction = targetConnection.BeginTransaction();

        var stagingCommitted = false;

        try
        {
            await DeleteExistingStagingRowsAsync(targetConnection, transaction, cargoId, cancellationToken);

            var stagingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Fretrack_Cargo_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_Cargo_Staging",
                    CargoHeaderSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });
                    },
                    cancellationToken),

                ["Fretrack_ShipmentService_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_ShipmentService_Staging",
                    ShipmentServiceSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_ShipmentPackages_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_ShipmentPackages_Staging",
                    ShipmentPackagesSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_ShipmentContainers_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_ShipmentContainers_Staging",
                    ShipmentContainersSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_Shipment_Routing_Staging_New"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_Shipment_Routing_Staging_New",
                    ShipmentRoutingSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_CargoEntities_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_CargoEntities_Staging",
                    CargoEntitiesSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_Invoices_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_Invoices_Staging",
                    InvoicesSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_VendorBill_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_VendorBill_Staging",
                    VendorBillSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
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
                    "dbo.Fretrack_ShipmentCharges_Staging",
                    ShipmentChargesSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_InvoiceLineItems_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_InvoiceLineItems_Staging",
                    InvoiceLineItemsSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_VendorBillLineItems_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_VendorBillLineItems_Staging",
                    VendorBillLineItemsSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken),

                ["Fretrack_HBL_Staging"] = await CopyQueryToStagingAsync(
                    sourceConnection,
                    targetConnection,
                    transaction,
                    "dbo.Fretrack_HBL_Staging",
                    HblSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
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
                    "dbo.Fretrack_CargoDocuments_Staging",
                    CargoDocumentsSql,
                    parameters =>
                    {
                        parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                    },
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CargoDocumentID"] = "FretrackCargoDocumentID",
                        ["DocumentTypeID"] = "FretrackDocumentTypeID"
                    })
            };

            await transaction.CommitAsync(cancellationToken);
            stagingCommitted = true;

            await ExecuteStoredProcedureAsync(targetConnection, cargoId, cancellationToken);

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

    private async Task DeleteExistingStagingRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int cargoId,
        CancellationToken cancellationToken)
    {
        var deleteStatements = new[]
        {
            "DELETE FROM dbo.Fretrack_Cargo_Staging WHERE CargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_ShipmentService_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM dbo.Fretrack_ShipmentPackages_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM dbo.Fretrack_ShipmentContainers_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM dbo.Fretrack_Shipment_Routing_Staging_New WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_CargoEntities_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_Invoices_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_ShipmentCharges_Staging WHERE FretrackCargoId = @CargoID",
            "DELETE FROM dbo.Fretrack_InvoiceLineItems_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_VendorBillLineItems_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_VendorBill_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_HBL_Staging WHERE FretrackCargoID = @CargoID",
            "DELETE FROM dbo.Fretrack_CargoDocuments_Staging WHERE CargoID = @CargoID"
        };

        foreach (var sql in deleteStatements)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                sql,
                parameters =>
                {
                    parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
                },
                cancellationToken);
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
        IReadOnlyDictionary<string, string>? columnRenames = null)
    {
        var table = await LoadDataTableAsync(sourceConnection, sql, addParameters, cancellationToken);
        if (table.Rows.Count == 0)
        {
            return 0;
        }

        if (columnRenames is not null && columnRenames.Count > 0)
        {
            ApplyColumnRenames(table, columnRenames);
        }

        using var bulkCopy = new SqlBulkCopy(targetConnection, SqlBulkCopyOptions.TableLock, transaction)
        {
            DestinationTableName = destinationTableName,
            BatchSize = table.Rows.Count
        };

        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
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

    private static async Task ExecuteStoredProcedureAsync(
        SqlConnection targetConnection,
        int cargoId,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand("dbo.usp_MigrateSingleCargoFromStaging", targetConnection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter("@FretrackCargoId", SqlDbType.Int) { Value = cargoId });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> CargoExistsInCargoMintAsync(
        SqlConnection targetConnection,
        int cargoId,
        string jobNo,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(CargoMintCargoExistsSql, targetConnection);
        command.Parameters.Add(new SqlParameter("@CargoID", SqlDbType.Int) { Value = cargoId });
        command.Parameters.Add(new SqlParameter("@JobNo", SqlDbType.NVarChar, 100) { Value = jobNo });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<DataTable> LoadDataTableAsync(
        SqlConnection connection,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, connection);
        addParameters?.Invoke(command.Parameters);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        Action<SqlParameterCollection>? addParameters,
        CancellationToken cancellationToken)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        addParameters?.Invoke(command.Parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string CargoHeaderSql = @"
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
    o.AccountID AS CompanyID,
    o.AccountName AS CompanyName,
    o.OpportunityOwnerID AS SalesPersonId,
    cd.NominationType,
    cd.AgentID AS OverseasAgentId,
    cd.AgentAddressID,
    cd.AgentName,
    cd.JobDate,
    cd.HandledBy,
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
    c.CustomerID AS CargoCustomerID,
    c.CargoApprovalStatus,
    c.CargoSOPApprovalStatus,
    c.OfficeID,
    c.FreightStatus,
    c.PaymentTerms,
    c.InvoicingParty,
    c.JobType,
    c.isHBLNOautogenerate,
    c.SalesQuoteID,
    c.isLocked,
    c.LockedBy,
    c.LockedDate,
    c.HblTerm,
    c.MblTerm,
    c.HblStatus,
    c.MblStatus,
    c.FreeDays,
    c.POR,
    c.isGstJob
FROM FretAssist_Copy.dbo.Cargo c
LEFT JOIN FretAssist_Copy.dbo.Opportunities o
    ON c.OpportunityID = o.OpportunityID
LEFT JOIN FretAssist_Copy.dbo.CargoDetails cd
    ON c.CargoID = cd.CargoID
WHERE c.JobNo = @JobNo;";

    private const string CargoMintCargoExistsSql = @"
SELECT COUNT(1)
FROM dbo.Cargo
WHERE CargoID = @CargoID
   OR JobNo = @JobNo;";

    private const string ShipmentServiceSql = @"
SELECT
    c.CargoID AS FretrackCargoId,
    c.ModeOfTransport,
    c.TransportDirection,
    c.isConsolidation,
    c.POLID,
    c.POL,
    c.PODID,
    c.POD,
    c.ETD,
    c.ETA,
    c.Notes,
    ar.AirCarrierID AS AirCarrierId,
    ar.AirCarrierName,
    orr.OceanCarrierID AS OceanCarrierId,
    orr.OceanCarrierName,
    CASE
        WHEN c.ModeOfTransport = 'Air' THEN ar.AirCarrierID
        WHEN c.ModeOfTransport = 'Ocean' THEN orr.OceanCarrierID
        ELSE NULL
    END AS CarrierId,
    c.CreatedBy,
    c.DateCreated,
    c.ModifiedBy,
    c.DateModified,
    c.DeletedBy,
    c.DateDeleted,
    c.isDeleted
FROM FretAssist_Copy.dbo.Cargo c
LEFT JOIN FretAssist_Copy.dbo.AirShipmentRouting ar
    ON c.CargoID = ar.ShipmentID
LEFT JOIN FretAssist_Copy.dbo.OceanShipmentRouting orr
    ON c.CargoID = orr.ShipmentID
WHERE c.CargoID = @CargoID;";

    private const string ShipmentPackagesSql = @"
SELECT p.*
FROM (
    SELECT
        p.CargoPackID AS FretrackPackId,
        p.CargoPackTypeID AS FretrackPackTypeID,
        p.CargoID AS FretrackCargoId,
        p.ContainerID,
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
        p.isDeleted
    FROM FretAssist_Copy.dbo.CargoPackages p
    WHERE p.CargoID = @CargoID
) p;";

    private const string ShipmentContainersSql = @"
SELECT
    p.ContainerCode,
    p.ContainerID AS FretrackContainerID,
    p.ContainerTypeID,
    p.isDeleted,
    p.VGMWeight,
    p.SalesQuoteID,
    p.CargoID AS FretrackCargoId,
    p.ContainerNumber,
    p.Seal1,
    p.Seal2,
    p.Description,
    p.CreatedBy,
    p.DateCreated,
    p.ModifiedBy,
    p.DateModified,
    p.DeletedBy,
    p.DateDeleted
FROM FretAssist_Copy.dbo.CargoContainers p
WHERE p.CargoID = @CargoID;";

    private const string ShipmentRoutingSql = @"
SELECT
    c.CargoID AS FretrackCargoID,
    RoutingMode = 'OCEAN',
    osr.OceanRoutingID AS FretrackRoutingID,
    RoutingOrder = osr.LegNumber,
    CarrierID = osr.OceanCarrierID,
    CarrierName = osr.OceanCarrierName,
    FlightOrVoyageNo = osr.VoyageNumber,
    VesselName = osr.VesselName,
    POLID = osr.POLID,
    POL = osr.POL,
    PODID = osr.PODID,
    POD = osr.POD,
    ETD = osr.ETD,
    ATD = osr.ATD,
    ETA = osr.ETA,
    ATA = osr.ATA,
    BookingNumber = osr.BookingNumber,
    BookingDate = osr.BookingDate,
    c.JobType,
    j.JOBTypeName,
    c.ModeOfTransport,
    c.TransportDirection,
    POT1ID = osr.POT1ID,
    POT1 = osr.POT1,
    POT2ID = osr.POT2ID,
    POT2 = osr.POT2,
    IsDeleted = osr.IsDeleted
FROM FretAssist_Copy.dbo.Cargo c
INNER JOIN FretAssist_Copy.dbo.OceanShipmentRouting osr
    ON c.CargoID = osr.ShipmentID
INNER JOIN FretAssist_Copy.dbo.JobTypeMaster j
    ON c.JobType = j.JOBTypeID
WHERE c.CargoID = @CargoID
UNION ALL
SELECT
    c.CargoID AS FretrackCargoID,
    RoutingMode = 'AIR',
    asr.AirRoutingID AS FretrackRoutingID,
    RoutingOrder = asr.RoutingOrder,
    CarrierID = asr.AirCarrierID,
    CarrierName = asr.AirCarrierName,
    FlightOrVoyageNo = asr.FlightNumber,
    VesselName = NULL,
    POLID = asr.POLID,
    POL = asr.POL,
    PODID = asr.PODID,
    POD = asr.POD,
    ETD = asr.ETD,
    ATD = asr.ATD,
    ETA = asr.ETA,
    ATA = asr.ATA,
    BookingNumber = asr.BookingNumber,
    BookingDate = asr.BookingDate,
    c.JobType,
    j.JOBTypeName,
    c.ModeOfTransport,
    c.TransportDirection,
    POT1ID = asr.POT1ID,
    POT1 = asr.POT1,
    POT2ID = asr.POT2ID,
    POT2 = asr.POT2,
    IsDeleted = asr.IsDeleted
FROM FretAssist_Copy.dbo.Cargo c
INNER JOIN FretAssist_Copy.dbo.AirShipmentRouting asr
    ON c.CargoID = asr.ShipmentID
INNER JOIN FretAssist_Copy.dbo.JobTypeMaster j
    ON c.JobType = j.JOBTypeID
WHERE c.CargoID = @CargoID;";

    private const string CargoEntitiesSql = @"
SELECT *
FROM (
    SELECT
        ce.CargoEntityID AS FretrackCargoEntityID,
        ce.CargoID AS FretrackCargoID,
        ce.CompanyID AS FretrackCompanyId,
        ce.EntityTypeID,
        ce.EntityName,
        ce.EntityAddressID AS FretrackEntityAddressID,
        ce.EntityCountryID,
        ce.EntityCountry,
        ce.EntityStateID,
        ce.EntityState,
        ce.EntityCityID,
        ce.EntityCity,
        ce.EntityAddressLine1,
        ce.EntityZipCode,
        ce.EntityDisplayText,
        ce.CreatedBy,
        ce.DateCreated,
        ce.ModifiedBy,
        ce.DateModified,
        ce.DeletedBy,
        ce.DeletedDate,
        ce.isPayor,
        ce.isDeleted,
        ce.CompanyName
    FROM FretAssist_Copy.dbo.CargoEntities ce
    WHERE ce.CargoID = @CargoID
) ce;";

    private const string InvoicesSql = @"
SELECT
    i.InvoiceID AS FretrackInvoiceID,
    i.CargoID AS FretrackCargoID,
    i.InvoiceNumber,
    i.InvoiceTypeGST,
    i.InvoiceType,
    i.InvoiceDate,
    i.PayingPartyID,
    i.PayingParty,
    i.PayingPartyAddress,
    i.CurrencyCode,
    i.ExchangeRate,
    i.SubTotal,
    i.TaxAmount,
    i.TotalAmount,
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
    i.CreatedBy,
    i.DateCreated,
    i.ModifiedBy,
    i.DateModified,
    i.DeletedBy,
    i.DateDeleted,
    i.isDeleted,
    i.InvoiceAmountWords,
    i.InvoiceApprovalStatus
FROM FretAssist_Copy.dbo.Invoices i
WHERE i.CargoID = @CargoID;";

    private const string ShipmentChargesSql = @"
SELECT
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
        MAX(CASE WHEN cc.IncomeExpense = 'INCOME' THEN cc.TotalAmount END),
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
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.TotalAmount END),
    FretrackBillToId =
        MAX(CASE WHEN cc.IncomeExpense = 'EXPENSE' THEN cc.PayingPartyID END),
    FretrackLineNumber = MIN(cc.LineNumber),
    FretrackCreatedBy = MAX(cc.CreatedBy),
    FretrackCreatedDate = MIN(cc.DateCreated),
    FretrackUpdatedBy = MAX(cc.ModifiedBy),
    FretrackUpdatedDate = MAX(cc.DateModified)
FROM FretAssist_Copy.dbo.CargoCharges cc
WHERE cc.CargoID = @CargoID
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
    invLines.ApplyPer AS FretrackApplyPer
FROM FretAssist_Copy.dbo.InvoiceLineItems invLines
INNER JOIN FretAssist_Copy.dbo.Invoices inv
    ON inv.InvoiceID = invLines.InvoiceID
WHERE inv.CargoID = @CargoID;";

    private const string VendorBillLineItemsSql = @"
SELECT
    bill.VendorBillID AS FretrackVendorBillID,
    bill.CargoID AS FretrackCargoID,
    bill.VendorBillNumber AS FretrackVendorBillNumber,
    VendorBillGSTTreatment = '',
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
    invitmbill.CreatedBy AS FretrackCreatedBy,
    invitmbill.ServiceCode,
    invitmbill.ApplyPer AS FretrackApplyPer
FROM FretAssist_Copy.dbo.VendorBillLineItems invitmbill
INNER JOIN FretAssist_Copy.dbo.VendorBill bill
    ON invitmbill.VendorBillID = bill.VendorBillID
WHERE bill.CargoID = @CargoID;";

    private const string VendorBillSql = @"
SELECT *
FROM FretAssist_Copy.dbo.VendorBill
WHERE IsDeleted = 0
  AND CargoID = @CargoID;";

    private const string HblSql = @"
SELECT *
FROM FretAssist_Copy.dbo.CargoHBL
WHERE CargoID = @CargoID;";

    private const string CargoDocumentsSql = @"
SELECT *
FROM FretAssist_Copy.dbo.CargoDocuments
WHERE CargoID = @CargoID
  AND ISNULL(isDeleted, 0) = 0;";
}
