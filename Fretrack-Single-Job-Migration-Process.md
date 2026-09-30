# Fretrack Single Job Migration Process

This document explains the process used by the single-job migration API that moves one Fretrack job into CargoMint staging tables.

## Purpose

The API migrates one job at a time from Fretrack into CargoMint staging.

It does not write to CargoMint main tables. It only populates staging tables.

The latest editable copy of the stored procedure is kept at `sql/usp_MigrateSingleCargoFromStaging.latest.sql`.

## API

`POST /api/fretrack-migration/single-job`

### Input

```json
{
  "jobNo": "PAE260012"
}
```

## High-Level Flow

1. Receive `JobNo`.
2. Find the matching cargo record in Fretrack.
3. Read the related `CargoID`.
4. Fetch all related Fretrack records using that `CargoID`.
5. Check whether the cargo already exists in CargoMint.
6. Map Fretrack source columns to CargoMint staging columns using explicit SQL aliases.
7. Delete any existing staging rows for the same cargo so the API can be rerun safely.
8. Insert data into CargoMint staging tables inside one transaction.
   Every staging table projection now includes `OrgId`, and the API passes the same `OrgId` value to each staging query.
9. Commit staging inserts.
10. Call the CargoMint stored procedure `dbo.usp_MigrateSingleCargoFromStaging` to update staging rows and insert actual tables.
11. After the procedure completes, run cargo-document migration for the same `CargoID` so the actual `Documents` table is populated and `Fretrack_CargoDocuments_Staging.CargoMintDocumentID` is backfilled for that cargo only:

```sql
UPDATE s
SET s.CargoMintDocumentID = d.DocId
FROM dbo.Fretrack_CargoDocuments_Staging s
JOIN dbo.Documents d
    ON d.FretrackDocumentID = s.FretrackCargoDocumentID
WHERE s.CargoMintDocumentID IS NULL
  AND s.CargoID = @CargoID;
```

The document insert path now writes:

- `DocFilePath`, `DocumentFTPLink`, and `DocumentLocalLink` as the CargoMint view URL
- `BlobName` as `FretrackDocuments/{fileName}`
- `DocTitle` as the clean file name
- `DocDescription` as the clean file name
- `DocFileType` from the file extension on `DocDescription`
- `EntityType` as `Shipment`
- `EntityId` from `CargoMintShipmentID`
- `FretrackCargoId` when the target table exposes that column
- The CargoMint-resolved ID columns in the staging load are intentionally left `NULL` where the stored procedure now resolves them from the raw Fretrack values.

If a `Documents` row already exists for the same cargo document, the migration updates that row with the refreshed metadata and Azure view URL instead of leaving the document-specific fields null.

The cargo-document insert path also guarantees a non-null `DocTitle` value by using the uploaded filename as the final source.

12. Return the inserted row counts per staging table.

## Source Lookup

The first lookup uses `JobNo` to find the cargo row:

```sql
SELECT *
FROM FretAssist_Copy.dbo.Cargo
WHERE JobNo = @JobNo;
```

From this row, the API uses:

- `CargoID`
- `JobNo`

## Staging Tables

The API inserts data into these tables:

- `dbo.Fretrack_Cargo_Staging`
- `dbo.Fretrack_ShipmentService_Staging`
- `dbo.Fretrack_ShipmentPackages_Staging`
- `dbo.Fretrack_ShipmentContainers_Staging`
- `dbo.Fretrack_Shipment_Routing_Staging_New`
- `dbo.Fretrack_CargoEntities_Staging`
- `dbo.Fretrack_Invoices_Staging`
- `dbo.Fretrack_InvoiceEsync_Staging`
- `dbo.Fretrack_ShipmentCharges_Staging`
- `dbo.Fretrack_InvoiceLineItems_Staging`
- `dbo.Fretrack_VendorBill_Staging`
- `dbo.Fretrack_VendorBillLineItems_Staging`
- `dbo.Fretrack_HBL_Staging`
- `dbo.Fretrack_CargoDocuments_Staging`

All staging tables above include `OrgId` in the inserted projection.

## Column Mapping Rules

All source-to-staging name differences are handled with explicit SQL aliases or explicit column mapping in code.

### 1. `Fretrack_ShipmentService_Staging`

- `CargoID` -> `FretrackCargoId`

### 2. `Fretrack_ShipmentPackages_Staging`

- `CargoPackTypeID` -> `FretrackPackTypeID`
- `CargoID` -> `FretrackCargoId`
- `CargoPackID` -> `FretrackPackId`

### 2a. `Fretrack_ShipmentService_Staging`

- `CargoID` -> `FretrackCargoId`
- The staging query does not require a `JobTypeMaster` match anymore; rows are loaded even when Fretrack `JobType` has no lookup value.

### 3. `Fretrack_ShipmentContainers_Staging`

- `CargoID` -> `FretrackCargoId`
- `ContainerID` -> `FretrackContainerID`

### 4. `Fretrack_Shipment_Routing_Staging_New`

- `CargoID` -> `FretrackCargoID`
- `RoutingID` -> `FretrackRoutingID`

### 5. `Fretrack_CargoEntities_Staging`

- `CargoID` -> `FretrackCargoID`
- `CompanyID` -> `FretrackCompanyId`
- `CargoEntityID` -> `FretrackCargoEntityID`
- `EntityAddressID` -> `FretrackEntityAddressID`
- `CompanyAddressId` is validated against `dbo.CompanyAddress` before `ShipmentParties` insert; invalid values are cleared to avoid FK failures.
- The CargoMint foreign-key helper columns for this staging table are now left `NULL` during the bulk copy step because the stored procedure resolves them later.

### 6. `Fretrack_Invoices_Staging`

- `InvoiceID` -> `FretrackInvoiceID`
- `CargoID` -> `FretrackCargoID`

### 7. `Fretrack_InvoiceLineItems_Staging`

- `InvoiceID` -> `FretrackInvoiceID`
- `CargoID` -> `FretrackCargoID`
- `InvoiceNumber` -> `FretrackInvoiceNumber`
- `InvoiceTypeGST` -> `FretrackInvoiceTypeGST`
- `InvoiceLineItemID` -> `FretrackInvoiceLineItemID`
- `ChargeItemID` -> `FretrackChargeItemID`
- `ChargeID` -> `FretrackChargeID`
- `ChargeDescription` -> `FretrackChargeDescription`
- `Quantity` -> `FretrackQuantity`
- `Rate` -> `FretrackRate`
- `CurrencyCode` -> `FretrackCurrencyCode`
- `ExRate` -> `FretrackExRate`
- `TaxPercent` -> `FretrackTaxPercent`
- `TaxAmount` -> `FretrackTaxAmount`
- `TaxableAmount` -> `FretrackTaxableAmount`
- `NonTaxableAmount` -> `FretrackNonTaxableAmount`
- `ExpectedAmount` -> `FretrackExpectedAmount`
- `CreatedBy` -> `FretrackCreatedBy`

### 7a. `Fretrack_InvoiceEsync_Staging`

- `InvoiceID` -> `FretrackInvoiceID`
- `CargoID` -> `FretrackCargoID`
- The API loads `InvoiceESync` rows only for cargos whose job number matches the configured FL/AE/SE/AI/SI 26 patterns.

### 8. `Fretrack_VendorBillLineItems_Staging`

- `VendorBillID` -> `FretrackVendorBillID`
- `CargoID` -> `FretrackCargoID`
- `VendorBillNumber` -> `FretrackVendorBillNumber`
- `VendorBillGSTTreatment` -> `FretrackVendorBillGSTTreatment`
- `VendorBillLineItemID` -> `FretrackVendorBillLineItemID`
- `ChargeItemID` -> `FretrackChargeItemID`
- `ChargeID` -> `FretrackChargeID`
- `ChargeDescription` -> `FretrackChargeDescription`
- `Quantity` -> `FretrackQuantity`
- `Rate` -> `FretrackRate`
- `CurrencyCode` -> `FretrackCurrencyCode`
- `ExRate` -> `FretrackExRate`
- `TaxPercent` -> `FretrackTaxPercent`
- `TaxAmount` -> `FretrackTaxAmount`
- `TaxableAmount` -> `FretrackTaxableAmount`
- `NonTaxableAmount` -> `FretrackNonTaxableAmount`
- `ExpectedAmount` -> `FretrackExpectedAmount`
- `CreatedBy` -> `FretrackCreatedBy`
- `ApplyPer` -> `FretrackApplyPer`

### 9. `Fretrack_VendorBill_Staging`

- `VendorBillID` -> `FretrackVendorBillID`
- `CargoID` -> `FretrackCargoID`
- `PayingPartyID` -> `FretrackPayingPartyID`
- `PayingPartyAddressID` -> `FretrackPayingPartyAddressID`

### 10. `Fretrack_HBL_Staging`

- `HBLID` -> `FretrackHBLID`
- `CargoID` -> `FretrackCargoID`

### 11. `Fretrack_CargoDocuments_Staging`

- `CargoDocumentID` -> `FretrackCargoDocumentID`
- `DocumentTypeID` -> `FretrackDocumentTypeID`

## Transaction Behavior

The migration runs inside one transaction on the CargoMint connection.

If any step fails:

- the transaction is rolled back
- no partial staging load is kept

## Rerun Behavior

Before inserting new staging rows, the API deletes existing staging rows for the same cargo so the job can be rerun safely.

## Response

The API returns:

- `JobNo`
- `CargoId`
- `CargoExistsInCargoMint`
- `ActualMigrationCompleted`
- `status`
- `message`
- staging row counts per table

### Example

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
    "Fretrack_ShipmentService_Staging": 1,
    "Fretrack_ShipmentPackages_Staging": 5,
    "Fretrack_ShipmentContainers_Staging": 2,
    "Fretrack_Shipment_Routing_Staging_New": 1,
    "Fretrack_CargoEntities_Staging": 4,
    "Fretrack_Invoices_Staging": 2,
    "Fretrack_ShipmentCharges_Staging": 8,
    "Fretrack_InvoiceLineItems_Staging": 10,
    "Fretrack_VendorBill_Staging": 3,
    "Fretrack_VendorBillLineItems_Staging": 6,
    "Fretrack_HBL_Staging": 2,
    "Fretrack_CargoDocuments_Staging": 4
  }
}
```

## Notes

- Fretrack is read only.
- CargoMintDB is used for staging inserts.
- Source and destination column names must be matched explicitly when they differ.
- The API is intended for one job per call.
- If CargoMint already has the cargo record, the existing staging rows are deleted first and then reinserted.
- After staging is committed, the API calls the CargoMint stored procedure to update staging rows and insert actual tables.
- The cargo document migration runs after the stored procedure call for the same `CargoID`, and the staging backfill is scoped to that cargo only.
