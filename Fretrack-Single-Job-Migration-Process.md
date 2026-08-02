# Fretrack Single Job Migration Process

This document explains the process used by the single-job migration API that moves one Fretrack job into CargoMint staging tables.

## Purpose

The API migrates one job at a time from Fretrack into CargoMint staging.

It does not write to CargoMint main tables. It only populates staging tables.

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
9. Commit staging inserts.
10. Call the CargoMint stored procedure `dbo.usp_MigrateSingleCargoFromStaging` to update staging rows and insert actual tables.
11. Return the inserted row counts per staging table.

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
- `dbo.Fretrack_ShipmentCharges_Staging`
- `dbo.Fretrack_InvoiceLineItems_Staging`
- `dbo.Fretrack_VendorBill_Staging`
- `dbo.Fretrack_VendorBillLineItems_Staging`
- `dbo.Fretrack_HBL_Staging`
- `dbo.Fretrack_CargoDocuments_Staging`

## Column Mapping Rules

All source-to-staging name differences are handled with explicit SQL aliases or explicit column mapping in code.

### 1. `Fretrack_ShipmentService_Staging`

- `CargoID` -> `FretrackCargoId`

### 2. `Fretrack_ShipmentPackages_Staging`

- `CargoPackTypeID` -> `FretrackPackTypeID`
- `CargoID` -> `FretrackCargoId`
- `CargoPackID` -> `FretrackPackId`

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
