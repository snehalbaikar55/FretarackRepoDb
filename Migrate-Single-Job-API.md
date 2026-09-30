# Migrate Single Job API

This document describes the single-job Fretrack migration API that stages one cargo job from Fretrack into CargoMint and then triggers the CargoMint stored procedure that updates staging and actual tables.

## Endpoint

`POST /api/fretrack-migration/single-job`

## Purpose

- Accept one `JobNo`
- Find the matching cargo in Fretrack
- Load all related Fretrack rows for that cargo
- Insert those rows into CargoMint staging tables
- Call the CargoMint stored procedure to move data from staging into actual tables

## Zoho Bill Lookup Flow

The API now uses one Zoho bill endpoint for both direct bill lookup and job-based lookup.

### Endpoint

`GET /api/vendor-bills/getBillFromZoho?billNumber={billNumber}`

or

`GET /api/vendor-bills/getBillFromZoho?jobNo={jobNo}`

### Flow

1. If `billNumber` is passed, call Zoho Books directly and return the raw bill response.
2. If `jobNo` is passed, resolve `CargoID` from the Fretrack `Cargo` table.
3. Read the related vendor bill numbers from the Fretrack `VendorBill` table for that cargo.
4. Call the same Zoho Books bill endpoint for each bill number.
5. Return the aggregated Zoho bill responses.

### Example

If the vendor bill table returns:

```json
{
  "cargo_id": 280387,
  "bill_numbers": [
    "EXP/20-21/0575",
    "LS-049 _21-22",
    "FI212745-1"
  ]
}
```

the API can use those bill numbers to fetch the matching bill data from Zoho Books.

## Zoho Invoice Lookup Flow

The API now uses one Zoho invoice endpoint for both direct invoice lookup and job-based lookup.

### Endpoint

`GET /api/vendor-bills/getInvoiceFromZoho?invoiceNumber={invoiceNumber}`

or

`GET /api/vendor-bills/getInvoiceFromZoho?jobNo={jobNo}`

### Flow

1. If `invoiceNumber` is passed, call Zoho Books directly and return the raw invoice response.
2. If `jobNo` is passed, resolve `CargoID` from the Fretrack `Cargo` table.
3. Read the related invoice numbers from the Fretrack `Invoices` table for that cargo.
4. Call the same Zoho Books invoice endpoint for each invoice number.
5. Return the aggregated Zoho invoice responses.

## Connections

- Source database: `DefaultConnection`
- Target database: `CargoMintDB`

## Request

```json
{
  "jobNo": "PAE260012"
}
```

## Response

On success, the API returns a migration summary similar to:

```json
{
  "jobNo": "PAE260012",
  "cargoId": 399931,
  "cargoExistsInCargoMint": false,
  "actualMigrationCompleted": true,
  "status": "success",
  "message": "New cargo staged successfully and actual tables were updated.",
  "stagingCounts": {
    "Fretrack_Cargo_Staging": 1,
    "Fretrack_ShipmentService_Staging": 1
  }
}
```

On failure, the API now returns the same response shape with `status: "failed"` and a clear `message` so the caller can see where the problem happened:

```json
{
  "jobNo": "PAE260012",
  "cargoId": 0,
  "cargoExistsInCargoMint": false,
  "actualMigrationCompleted": false,
  "status": "failed",
  "message": "Zoho bill fetch failed for JobNo 'PAE260012' with HTTP 500: <error response>",
  "stagingCounts": {}
}
```

## Flow

1. Read `JobNo` from the request.
2. Look up the cargo in Fretrack using `JobNo`.
3. Call the Zoho bill and invoice lookup APIs using the same `JobNo`.
4. Upsert the returned Zoho data into the GST mapping tables.
5. Read `CargoID` from the Fretrack cargo row.
6. Check whether the cargo already exists in CargoMint.
7. Resolve source and target schemas dynamically.
8. Remove any existing staging rows for the same `CargoID`.
9. Insert the Fretrack cargo header row into `Fretrack_Cargo_Staging`.
10. Insert related rows into the other staging tables.
11. Validate CargoMint foreign-key references before bulk insert where needed.
12. Commit the staging load in one transaction.
13. Call `dbo.usp_MigrateSingleCargoFromStaging`.
14. After the procedure completes, run cargo-document migration for the same `CargoID` so the actual `Documents` table is populated and `Fretrack_CargoDocuments_Staging.CargoMintDocumentID` is updated for that cargo only.
15. Return row counts and status.

The cargo-document insert path now writes:

- `DocFilePath`, `DocumentFTPLink`, and `DocumentLocalLink` using the CargoMint view URL
- `BlobName` as `FretrackDocuments/{fileName}`
- `DocTitle` as the clean file name
- `DocDescription` as the clean file name
- `DocFileType` from the file extension on `DocDescription`
- `EntityType` as `Shipment`
- `EntityId` from `CargoMintShipmentID`
- `FretrackCargoId` when the target table exposes that column

If the document already exists in `Documents`, the migration now updates the existing row with the same metadata instead of leaving `DocFileType`, `EntityType`, or `EntityId` null.

For bill documents (`DocTypeId = 8`), the migration also backfills matching `Bills` rows for the same shipment by setting `DocumentId` and `DocumentPath` when the bill invoice number appears in `DocDescription`.

The cargo-document insert path also guarantees a non-null `DocTitle` by using the uploaded filename as the final source.

## Staging Tables Loaded

The API currently loads these staging tables:

- `Fretrack_Cargo_Staging`
- `Fretrack_ShipmentService_Staging`
- `Fretrack_ShipmentPackages_Staging`
- `Fretrack_ShipmentContainers_Staging`
- `Fretrack_Shipment_Routing_Staging_New`
- `Fretrack_CargoEntities_Staging`
- `Fretrack_Invoices_Staging`
- `Fretrack_InvoiceEsync_Staging`
- `Fretrack_ShipmentCharges_Staging`
- `Fretrack_InvoiceLineItems_Staging`
- `Fretrack_VendorBill_Staging`
- `Fretrack_VendorBillLineItems_Staging`
- `Fretrack_HBL_Staging`
- `Fretrack_CargoDocuments_Staging`

## Behavior Rules

- If the cargo already exists in CargoMint, the staging rows for that `CargoID` are deleted first and then reinserted.
- If the cargo does not exist in CargoMint, the API still stages the Fretrack data and then calls the stored procedure.
- The API uses explicit SQL aliases and explicit column mapping whenever source and staging column names differ.
- The staging load runs inside one transaction on the CargoMint connection.
- If staging fails, the transaction is rolled back.

## Validation Rules

The API clears invalid target-side references before bulk insert when the target row would otherwise fail a foreign-key check:

- Invalid quotation references are cleared from `CargoMintQuotationId`
- Invalid user references are cleared from the relevant `CreatedBy` fields used by the CargoMint insert path

This prevents the stored procedure from failing on missing `SalesQuotations` or `Users` rows.

## Notes on Actual Tables

This API does not directly insert into CargoMint actual tables.

Instead:

1. The API stages the data.
2. The API calls `dbo.usp_MigrateSingleCargoFromStaging`.
3. The stored procedure updates staging rows and inserts or updates actual tables in CargoMint.
4. The API then runs cargo-document migration for the same cargo and backfills `CargoMintDocumentID` on the document staging rows for that cargo only.

## Error Handling

Common failures include:

- Job number not found in Fretrack
- Missing or mismatched source column names
- Missing target foreign-key references in CargoMint
- Transaction or stored procedure errors

When a failure happens, the API returns the underlying exception message instead of a generic `An error occurred while migrating single cargo` response. That makes it easier to tell whether the issue was in Zoho lookup, source data, staging, or the stored procedure.

## When to Use

Use this API when you want to migrate one specific Fretrack job end-to-end and verify the result before running larger migration batches.
