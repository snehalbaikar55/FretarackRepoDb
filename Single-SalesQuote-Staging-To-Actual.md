# Sales quote: staging mappings and actual quotation migration

## Procedure

Deploy `sql/usp_MigrateSingleSalesQuoteFromStaging.sql` into CargoMintDB using SSMS. The script uses CREATE OR ALTER and does not execute a migration when deployed. Requires SQL Server 2016 SP1 or newer, database compatibility level 130+, and the existing staging/master/target tables listed below.

The procedure is independent of the existing job-migration procedure. No existing procedure is replaced. The sales quote staging sync API does not automatically invoke it.

Flow:

1. Call POST `/api/fretrack-migration/single-salesquote` with dryRun=false to populate the four staging tables.
2. Execute `dbo.usp_MigrateSingleSalesQuoteFromStaging` with @DryRun=1 and the correct CargoMint master IDs/status values.
3. Review returned totals and resolve any missing mapping errors.
4. Execute with @DryRun=0 to commit the mapping updates and actual quotation records together.

No database deployment or live migration was performed during script creation. INSERT column names were checked against the supplied Cargomint schema; that check does not establish that your live database matches it.

## What it creates

| Source staging | Actual destination |
|---|---|
| Fretrack_SalesQuote_New | SalesQuotations |
| Fretrack_SalesQuoteDetails_New | SalesQuotationServiceDetails, one per source detail |
| Same detail | SalesQuotationRateOptions, one per created service |
| Fretrack_SalesQuoteCharges_New | SalesQuotationLineItems, combining buy/sell sides |
| Fretrack_SalesQuoteNomDetails | NominationType on SalesQuotations |

This implements one service and one rate option per detail, with Buy/Sell charge combination as confirmed. If detail rows actually represent alternative offers for the same service, revise the grouping before use rather than summing alternatives as separate services.

SalesQuotationContainers and SalesQuotationPackages are not populated: the four supplied staging schemas do not have package/container counts, types or dimensions. Header GrossWeight/Volume alone cannot establish these rows. Supply the source package/container records to extend the procedure.

Target IDs are captured with OUTPUT inserted. The actual target ID columns must have IDENTITY or an existing DEFAULT/sequence that generates values. The procedure does not add IDENTITY anywhere and never uses MAX(ID)+1. Staging ID columns remain plain int.

## Required inputs

| Parameter | Meaning |
|---|---|
| @SalesQuoteNumber | Staged Fretrack quotation number |
| @OrgId | CargoMint organization; use the intended org explicitly |
| @QuotationCurrencyId | CargoMint header currency ID |
| @LocalCurrencyId | Organization's configured local currency ID |
| @HeaderExRate | Local currency per unit of header currency; must be 1 if both currencies match |
| @ServiceTypeId | CargoMint service type for these detail rows |
| @TransportModeId | CargoMint transport mode |
| @TransportDirectionId | CargoMint transport direction |
| @QuotationStatus | Explicit accepted CargoMint quotation status; source flags/status are not assumed equivalent |
| @ServiceStatus | Explicit accepted service status |
| @OptionStatus | Explicit accepted option status |
| @ContactId | Optional override; otherwise staged CargoMintContactID must already be resolved |
| @CommodityId | Required when source Commodity contains text; choose the correct target master |
| @IncoTermId | Optional override; a nonzero source IncoTermID must have a staged/explicit mapping |
| @DefaultApplyPerId | Explicit fallback for missing or unknown source units; optional if all units resolve |
| @DryRun | Defaults to 1; 0 commits, 1 performs inserts/mappings and rolls back |

The single service-type/mode/direction inputs apply to every source detail in this quotation. Use this version only for quotes where that is correct. The source header labels are retained as TransportMode/TransportDirection text; choose corresponding target master IDs.

Use these read-only queries to inspect relevant target masters, and verify statuses from your normal CargoMint quotation save flow:

```sql
SELECT * FROM dbo.Currencies;
SELECT * FROM dbo.ServiceTypes;
SELECT * FROM dbo.TransportModes;
SELECT * FROM dbo.TransportDirections;
SELECT * FROM dbo.ApplyPers;
SELECT * FROM dbo.TaxGroups;
SELECT TOP (20) * FROM dbo.CompanyContacts;
SELECT TOP (20) * FROM dbo.CommodityMaster;
SELECT TOP (20) * FROM dbo.IncoTermMaster;
```

The supplied schema does not establish automatic contact, commodity, or Incoterm source-master mapping. Those IDs must be provided explicitly or pre-resolved in staging; no master records are created.

## Execution template

Replace every NULL below with verified values where required. Leaving required values NULL produces a validation error and rolls back. Status values must be confirmed from your CargoMint application.

```sql
DECLARE @OrgId int = NULL;
DECLARE @CurrencyId int = NULL;
DECLARE @LocalCurrencyId int = NULL;
DECLARE @HeaderRate decimal(22,8) = NULL;
DECLARE @ServiceTypeId int = NULL;
DECLARE @ModeId int = NULL;
DECLARE @DirectionId int = NULL;
DECLARE @ContactId int = NULL;
DECLARE @CommodityId int = NULL;
DECLARE @ApplyPerId int = NULL;
DECLARE @QuotationStatus nvarchar(50) = NULL;
DECLARE @ServiceStatus nvarchar(50) = NULL;
DECLARE @OptionStatus nvarchar(50) = NULL;

EXEC dbo.usp_MigrateSingleSalesQuoteFromStaging
    @SalesQuoteNumber = 'SQCOU-M252600034',
    @OrgId = @OrgId,
    @QuotationCurrencyId = @CurrencyId,
    @LocalCurrencyId = @LocalCurrencyId,
    @HeaderExRate = @HeaderRate,
    @ServiceTypeId = @ServiceTypeId,
    @TransportModeId = @ModeId,
    @TransportDirectionId = @DirectionId,
    @QuotationStatus = @QuotationStatus,
    @ServiceStatus = @ServiceStatus,
    @OptionStatus = @OptionStatus,
    @ContactId = @ContactId,
    @CommodityId = @CommodityId,
    @DefaultApplyPerId = @ApplyPerId,
    @DryRun = 1;
```

To commit, repeat with @DryRun=0. Do not wrap this procedure in another transaction: it owns its transaction and deliberately rejects nested calls. A dry-run ID is only a preview; identity/sequence values can be consumed even after rollback and must not be used as committed IDs.

## Mapping sources reused from the existing job procedure

| Mapping | Existing target/master source |
|---|---|
| Customer, paying party, nomination agent | Companies.FretrackCompanyIDs CSV → Companies.CompanyId, scoped to OrgId |
| PreparedBy, salesperson, audit users | Fretrack_UserMaster_Staging.FretrackUserId → CargoMintUserID |
| POL, POD and other staged route locations | Fretrack_Location_Master.LocationID → CargoMintLocationId |
| AIR carrier | Fretrack_AirlineMaster_Staging.AirlineID → CargoMintCarrierId |
| OCEAN/SURFACE carrier | Fretrack_OceanLineMaster_Staging.OceanLineID → CargoMintCarrierId |
| Charge currency | Currencies.CurrencyCode → CurrencyId |
| Charge item | Fretrack_Staging_CC_CM_Mapped.FretrackChargeItemName → CargoMintChargeItemID |
| Tax | TaxGroups.TotalRate → TaxGroupId; ambiguous rates require explicit staged CargoMintTaxRateID |
| ApplyPer | Same fixed unit-code mappings as the job procedure; confirm IDs match this database |

Existing explicit staged mappings are preserved except the explicit header currency/org/contact/Incoterm parameters. Header/customer/contact and charge paying-party scope are checked. Target foreign keys validate remaining references; the procedure does not replace full application business validation. Ambiguous company mappings are rejected. Unresolved source users needed by target records cause an error rather than silently copying Fretrack IDs.

OrgSettings is not queried by this version: @LocalCurrencyId must match your organization's configured local currency. Confirm header and line exchange rates all use that same local base. A local-currency charge must have ExRate=1.

## Buy/sell pairing

Active charge rows are grouped using:

`SQDetailRowID + ChargeItemID + LineNumber + normalized ApplyPer`

Each group may contain at most one INCOME row and one EXPENSE row. Multiple same-side rows cause a validation error. This avoids an incorrect many-to-many join. NULL LineNumber is allowed only when the rest of the key remains unique per side. A side without a counterpart is retained as a sell-only or buy-only target line; no charge is silently discarded. Source paying parties may differ between sides.

| INCOME | Target sell fields |
|---|---|
| Currency / ExRate / Rate / Quantity / tax | SellCurrencyId / SellExRate / SellRate / SellQuantity / SellTax* |
| mapped PayingPartyID | InvoiceToId |

| EXPENSE | Target buy fields |
|---|---|
| Currency / ExRate / Rate / Quantity / tax | BuyCurrencyId / BuyExRate / BuyRate / BuyQuantity / BuyTax* |
| mapped PayingPartyID | BillToId |

BuyLineItemId remains NULL: it is not the Fretrack expense ID. Both original charge rows link back to the same target SellLineItemId using CargoMintSQChargeID in staging.

## Calculations

Following the inspected CargoMint QuotationFinancials service:

- Persist Rate and TaxPercent at target scale 2 and Quantity at scale 4.
- Header-currency subtotal = rate × quantity × line exchange rate / header exchange rate.
- Tax = unrounded header-currency subtotal × tax percent / 100, rounded to 2 decimals.
- Store subtotal rounded to 2 decimals; total = stored subtotal + stored tax.
- Local amounts = corresponding header-currency amounts × header-to-local rate, rounded to 4 decimals.
- Option and service totals include tax. Header totals sum the one option per service.
- Profit = sales total − buying total; profit percentage = profit / sales total × 100 when sales total > 0.

Source TaxAmount, TotalAmount and TotalNonTaxableAmount are retained in staging for reconciliation and are not blindly copied into target totals. NULL TaxPercent is rejected. NULL source TaxAmount is recalculated from the provided tax percent, following the normal CargoMint calculation behavior. Arithmetic overflow causes rollback; no values are clipped.

For the supplied sample with INR as header/local currency and HeaderExRate=1: sell subtotal is 794 × 94 = 74,636; buy subtotal is 54,107; buy tax at 18% is 9,739.26; buy total is 63,846.26; profit after tax is 10,789.74. Confirm this intended tax treatment before committing. If different header currency is selected, totals are converted and rounded accordingly.

## Staging IDs updated

- Header CargoMintSalesQuoteID → new QuotationId.
- Header CargoMintCompanyID, CargoMintContactID, CargoMintSalesPersonID, CargoMintPreparedBy and available audit IDs → mapped references.
- Header CargoMintOfficeID → OrgId; CargoMintSalesQuoteCurrencyID → supplied header currency.
- Detail CargoMintSalesQuoteID → QuotationId; CargoMintSalesQuoteDetailID → QuotationServiceId; available location/carrier mapping columns resolved.
- Active charge CargoMintSQID → QuotationId; CargoMintSQDetailRowID → QuotationServiceId; CargoMintSQChargeID → combined SellLineItemId, plus master/user mappings.
- Nomination CargoMintSQID → QuotationId; available agent/audit mappings resolved.

There is no staging rate-option mapping column; option IDs are captured in a temporary map within the procedure. The actual relationships retain those IDs after commit. Unused mapping fields such as CargoMintRevisionID, CargoMintFileID, CargoMintVesselID, CargoMintSqNomDetailID and CargoMintSalesQuoteChargeID are not fabricated.

InternalNotes/BusinessLostNotes, source addresses, intermediate ports, vessel details, nomination agent/address detail and other fields without dedicated actual-table columns remain in staging. Detail Notes are preserved in unlimited OptionNotes; ServiceNotes is only populated when it fits 255 characters. This version does not promise a destination for every legacy field.

## Transaction behavior and verification

Mapping updates and target inserts use a single local transaction. Errors roll back both. The shared application lock coordinates with the staging API. Manual writers must be kept out of the staging dataset during migration. An already populated CargoMintSalesQuoteID or existing target quotation number is rejected rather than inserted twice; automatic updates of migrated quotations are not supported.

Before production, deploy and compile in a test CargoMint database, then check: valid dry run, persisted sample, ambiguous mapping, duplicate charge pairing, missing currency/tax/contact, foreign-key failure rollback, concurrent requests, repeated invocation, local/foreign header currency, and missing one-side charges. Inspect all staged target IDs and actual totals after commit.

The procedure has not been compiled or executed by SQL Server here. No live credentials were available. Static schema checks cannot validate actual master table contents, foreign keys, ID defaults, triggers or status semantics.

References: [CREATE PROCEDURE](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-procedure-transact-sql), [transaction-owned application locks](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql).
