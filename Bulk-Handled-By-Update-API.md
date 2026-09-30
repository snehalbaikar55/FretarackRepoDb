# Bulk Handled-By Update API

This document describes the new bulk handled-by update endpoint added for Fretrack staging and CargoMint shipments.

## Endpoint

`POST /api/fretrack-migration/bulk-handled-by-id`

## Purpose

- Accept one or more `JobNo` values
- Resolve Fretrack cargo rows when only job numbers are provided
- Update `Fretrack_Cargo_Staging.CargoMintHandledById` from the source `HandledById`
- Update `Shipments.HandledById` from the staging value
- Process all selected cargo rows inside one transaction

## Request

Array of `jobNos`

### Example

```json
{
  "jobNos": ["PAE260012", "PAE260013"]
}
```

## Behavior

1. The API reads the request body.
2. It resolves cargo IDs from `jobNos` using the Fretrack `Cargo` table.
3. For each cargo ID, it reads `HandledBy` from `dbo.CargoDetails`.
4. It backfills `dbo.Fretrack_Cargo_Staging.HandledById` when needed.
5. It updates `dbo.Fretrack_Cargo_Staging.CargoMintHandledById` from the handled-by value using `dbo.Fretrack_UserMaster_Staging`.
6. It updates `dbo.Shipments.HandledById` from the staging value for the same cargo.
7. It commits all updates in one transaction.

## Response

On success, the API returns a summary object with:

- `status`
- `message`
- `items`
- `totalCargoCount`
- `totalStagingRowsUpdated`
- `totalShipmentRowsUpdated`

### Example

```json
{
  "status": "success",
  "message": "Updated handled-by values for 2 cargo record(s).",
  "items": [
    {
      "cargoId": 280387,
      "jobNo": "PAE260012",
      "stagingRowsUpdated": 1,
      "shipmentRowsUpdated": 1
    },
    {
      "cargoId": 280388,
      "jobNo": "PAE260013",
      "stagingRowsUpdated": 1,
      "shipmentRowsUpdated": 1
    }
  ],
  "totalCargoCount": 2,
  "totalStagingRowsUpdated": 2,
  "totalShipmentRowsUpdated": 2
}
```

On failure, the API returns:

```json
{
  "status": "failed",
  "message": "Reason for the failure",
  "items": []
}
```

## Notes

- This endpoint is separate from the existing single-job migration flow.
- It does not stage cargo data or call `usp_MigrateSingleCargoFromStaging`.
- It only updates handled-by values in staging and shipments.
