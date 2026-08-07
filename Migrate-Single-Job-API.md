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

The API returns a migration summary similar to:

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

## Flow

1. Read `JobNo` from the request.
2. Look up the cargo in Fretrack using `JobNo`.
3. Read `CargoID` from the Fretrack cargo row.
4. Check whether the cargo already exists in CargoMint.
5. Resolve source and target schemas dynamically.
6. Remove any existing staging rows for the same `CargoID`.
7. Insert the Fretrack cargo header row into `Fretrack_Cargo_Staging`.
8. Insert related rows into the other staging tables.
9. Validate CargoMint foreign-key references before bulk insert where needed.
10. Commit the staging load in one transaction.
11. Call `dbo.usp_MigrateSingleCargoFromStaging`.
12. After the procedure completes, run cargo-document migration for the same `CargoID` so the actual `Documents` table is populated and `Fretrack_CargoDocuments_Staging.CargoMintDocumentID` is updated for that cargo only.
13. Return row counts and status.

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

## When to Use

Use this API when you want to migrate one specific Fretrack job end-to-end and verify the result before running larger migration batches.
