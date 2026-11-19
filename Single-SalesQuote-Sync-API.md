# Single sales quote sync API

Implemented directly in the existing RepoDbApi project.

## Files

- Controllers/SalesQuoteSyncController.cs
- Contracts/SalesQuoteSyncContracts.cs
- Services/SalesQuoteSyncService.cs (includes ISalesQuoteSyncService)
- Repositories/SalesQuoteSyncRepository.cs (includes ISalesQuoteSyncRepository)
- Existing Models/SalesQuote_New.cs, SalesQuoteDetails_New.cs, SalesQuoteNomDetails.cs and SalesQuoteCharges_New.cs are reused unchanged.
- Program.cs includes the two new dependency injection registrations. Existing job migration code is unchanged.

## Endpoint

```http
POST /api/fretrack-migration/single-salesquote
Content-Type: application/json
```

```json
{
  "salesQuoteNumber": "SQCOU-M252600034",
  "orgId": 18,
  "dryRun": true
}
```

Supply the correct positive orgId (18 above is an example). Set dryRun=false to save staging rows and mapping updates. The endpoint is discovered by the existing controllers and Swagger configuration. Run the existing RepoDbApi project using its normal launch profile. No new package references or configuration files are needed.

## Database settings

Uses existing connection strings: DefaultConnection for Fretrack and CargoMintDB for the target. Optional SalesQuoteSync:CommandTimeoutSeconds defaults to 120. Do not commit credentials.

## Source and destination

| Fretrack source | Existing Cargomint staging table | Child quote key |
|---|---|---|
| dbo.SalesQuote_New | dbo.Fretrack_SalesQuote_New | SalesQuoteID |
| dbo.SalesQuoteDetails_New | dbo.Fretrack_SalesQuoteDetails_New | SalesQuoteID |
| dbo.SalesQuoteNomDetails | dbo.Fretrack_SalesQuoteNomDetails | SQID |
| dbo.SalesQuoteCharges_New | dbo.Fretrack_SalesQuoteCharges_New | SQID |

The quote header is selected by SalesQuoteNumber, then children by the selected SalesQuoteID. Multiple matching headers, deleted headers, duplicate child IDs and charge detail references outside the quote are rejected. SalesQuoteCharges_New is used rather than combining it with SalesQuoteChargeItems; confirm this is the authoritative table for the quote.

Source IDs, values, NULLs and audit columns are preserved. After initial staging inserts, the API calls dbo.usp_UpdateSalesQuoteStagingMappings with SalesQuoteID and OrgId on the same connection and transaction. Matching CargoMint master mapping fields are filled; unmatched fields retain their existing values, which can be NULL. No IDENTITY columns or new database tables are created. Source tables are never updated.

This API populates the four staging tables only. It does not write to the six production SalesQuotation tables. ActualMigrationCompleted is therefore false in the response, even when staging status is synced.

## Behavior

- RepoDb performs parameterized reads and duplicate checks. SqlBulkCopy writes staging using the same approach as the single-job migration repository.
- One serializable read transaction captures the source aggregate. It may briefly block source writers.
- One serializable target transaction saves all four batches and executes the mapping procedure before committing. A transaction-owned application lock coordinates requests through this API. SQL Server SELECT/INSERT and application-lock permissions are required.
- Dry run performs transactional bulk inserts and rolls back, validating schema and constraints. Bulk copy preserves NULLs and checks constraints; it does not fire insert triggers.
- Identical staged source values rerun the mapping procedure and return already_synced. They do not insert duplicate source rows. Matching master mappings are refreshed by the procedure; unmatched values are retained.
- Partial, changed or conflicting staging records return HTTP 409. No automatic overwrite or deletion occurs.
- New source snapshots are not applied to already-staged changed quotations; review such conflicts manually.
- Source and target transactions are separate, not a distributed transaction. Target represents the captured source state.
- Use only one Fretrack source database per staging dataset. Avoid concurrent manual staging imports.

## Response

The default chargesOnly=true response contains salesQuoteNumber, orgId, importScope, serviceTypeId, serviceLabel, transportModeId, transportMode, transportDirectionId, transportDirection and chargeLines. It omits staging counters, full quotation data and table arrays. Set chargesOnly=false to receive the earlier full response with status, dryRun and staging metadata. Dry-run requests still roll back, even though the compact body does not include a dryRun flag.

HTTP codes: 400 invalid request; 404 source not found; 409 conflict or unavailable application lock; 503 unexpected/database failure with traceId. SQL diagnostics and credentials are not returned.

The endpoint uses the host project's existing security behavior; no standalone API-key middleware was copied. Apply your deployment's authentication/authorization before external exposure.

## Validation

Build: dotnet build RepoDbApi.csproj --no-restore

Before production, validate against a test database: missing quote, dry-run rollback, successful row counts, identical replay, conflict after modifying staging, concurrent calls, and rollback on a target constraint failure. The configured development database flow has been verified with dryRun=true; persisted sync and browser integration remain untested.


## Deploy the mapping procedure before running the updated API

Run `sql/usp_UpdateSalesQuoteStagingMappings.sql` in CargoMintDB to CREATE OR ALTER the procedure. Redeploy this updated script even if the procedure already exists: it now supports being called within the API transaction using a savepoint. It still works when executed standalone.

The API requires EXECUTE permission on dbo.usp_UpdateSalesQuoteStagingMappings and the procedure needs access to the master tables used by its UPDATE statements. The procedure must be deployed before calling this updated endpoint. A missing or older transaction-rejecting procedure causes the API to fail and roll back new staging rows.

Sequence: read Fretrack → insert four staging batches (or validate identical existing staging) → execute mapping procedure → commit. For dryRun=true, both inserts and mapping changes are rolled back. If the procedure fails, the API rolls back the complete target transaction. Source records and actual quotation tables remain unchanged.

The procedure maps only the fields listed in its four updates. Tax, vessel, office, type-of-move and generated production IDs are not resolved by this simple mapping procedure. The separate actual-migration procedure is not invoked automatically.


## Full-quotation prefill response (chargesOnly=false)

After executing the staging-mapping procedure, the API rereads the four staging tables inside the same transaction and returns `quotation` using the property names consumed by CargoMint's existing `patchQuotationForm(data)` method:

```text
response.quotation
  salesQuotationServiceDetails[]
    salesQuotationRateOptions[]
      salesQuotationLineItems[]
```

The previous sync metadata remains in the response. Additional fields are `quotation` and `requiresReview`. Under `quotation`, `prefillMissingFields` contains field paths and review messages for unresolved or ambiguous data. Receiving a payload does not mean it is ready to save: inspect these fields and complete the form first.

For full-quotation prefill only, set chargesOnly=false and use your existing quotation editor method after its master dropdowns/bootstrap data have loaded. For the default charge-only import, use the Fretrack dialog and selected existing service/option; do not patch the whole quotation form:

```typescript
this.http.post<any>(`${migrationApiUrl}/api/fretrack-migration/single-salesquote`, {
  salesQuoteNumber: quoteNumber,
  orgId: this.currentOrgId,
  chargesOnly: false,
  dryRun: false
}).subscribe(response => {
  this.patchQuotationForm(response.quotation);
  // Present response.quotation.prefillMissingFields for review when requiresReview is true.
});
```

The component variable names for URL, quote number and org context must match your frontend. The existing patchQuotationForm method rebuilds nested FormArrays and resolves dropdown objects. Calling only salesQuotationForm.patchValue(response) will not create the required service/option/line controls. No frontend files were changed by this backend feature.

### Payload rules

- Source SalesQuoteID stays in response metadata; quotationId and child target IDs remain NULL for a new quotation. A source already linked to an actual CargoMint quotation returns 409; load the actual quotation instead of recreating it from staging.
- Header fields include target customer/contact/salesperson/currency IDs, source dates, labels, terms and display flags. New prefills start as QUOTATION_DRAFT, matching the CargoMint editor's draft state; source approval is not treated as CargoMint workflow approval.
- One detail becomes one service with one rate option. Active INCOME/EXPENSE charges are paired by source detail + charge item + line number + normalized source unit. Duplicate same-side charges are rejected. Unpaired charges remain one-sided lines; deleted charges are excluded.
- Only mapped CargoMint IDs are used as target references. Companies are checked against the target organization; source IDs are never used as a fallback. Unresolved references stay NULL.
- Service/mode IDs are resolved only from an unambiguous Fretrack_Service_Mode_Mapping entry matching the source quote type and transport mode. Direction and commodity are resolved by unambiguous target master matches. Service and option statuses remain unset and are explicitly flagged for selection.
- Unit aliases resolve against active target ApplyPer names; missing/ambiguous units stay NULL. Tax IDs use valid staged mappings or a unique active target tax group matching the percent. These extra prefill lookups do not alter staging columns.
- The local currency follows settings.OrgSettings.DefaultCurrencyCode (INR when no code is configured). Header exchange rate is 1 only when header/local currencies match, or is taken from one unambiguous positive source charge rate for the same mapped header currency. Missing/ambiguous header rates are not invented.
- Rates and tax percentages are rounded to the target's scale 2. Amounts use CargoMint's header-currency formula; subtotals/tax/totals are previews and the normal CargoMint save service remains authoritative. Missing inputs keep monetary previews NULL. Source TotalAmount is not blindly copied into a target amount.
- No package/container rows are fabricated. These arrays are empty and cargo requirements are flagged for review. Unsupported legacy fields remain in staging.
- DryRun=true also returns quotation, built before rollback; neither staging nor mapping changes persist. With dryRun=false, response is returned after the complete target transaction commits.

### Additional read permissions for prefill

The CargoMint login now also needs SELECT access to dbo.Fretrack_Service_Mode_Mapping, dbo.TransportDirections, dbo.ApplyPer, dbo.TaxGroups, dbo.CommodityMaster, dbo.Currencies, dbo.Companies and settings.OrgSettings. These names were checked against the local CargoMint source models; confirm that the deployed database schema matches. Missing table/permission errors roll back new staging writes, as with other transaction failures.

The DTO names and nesting were checked against the local CargoMint SalesQuotationSaveDto and sq-edit.component.ts patchQuotationForm method. The configured development SQL flow has been verified with dryRun=true. Browser integration and committed sync still require testing in your environment.


## Diagnosing sync failures

HTTP 503 responses now include stage, sqlErrorNumber, sqlProcedure, sqlLineNumber, errorType and traceId, plus a sanitized message. Swagger documents the 503 schema. Full exceptions are written to server-side logs for the matching traceId; raw SQL error messages and row values are not returned to clients.

Examples: SQL 51101 means an older deployed mapping procedure rejected the API transaction; redeploy the current mapping script. SQL 208 identifies a missing table/view. The unit master is dbo.ApplyPer (singular), verified against the configured live database metadata.


### Verified 503 fixes (2026-11-12)

Read-only live metadata identified the unit table as dbo.ApplyPer, not dbo.ApplyPers, and showed the deployed mapping procedure still rejected caller-owned transactions. The query was corrected and the transaction-compatible procedure was deployed to the database configured by the local development settings. The original procedure definition was backed up locally before deployment; mapping queries were unchanged.

A complete repository-level dry run for the supplied quote succeeded: source read, four staging batches, mapping procedure, nested quotation projection and JSON serialization. It produced one service and one combined buy/sell line; StagingMappingsUpdated was false and target staging row counts were unchanged after rollback. No production quotation records were created. The payload reported RequiresReview=true for unresolved form fields; this is separate from sync execution success.

Restart the API process to load the corrected binary before retrying Swagger. If your running server uses different environment-variable connection strings or another database, deploy the same current procedure script to that target as well.


### Header mapping coverage

The staging mapping procedure now resolves ContactID through CompanyContacts.FretrackContactID for the mapped customer/org; IncoTermID through Fretrack_IncoTerm_Master_Staging; both header currency IDs through Fretrack_CurrencyMaster_Staging with currency-code fallback; and company/pickup/delivery address IDs through CompanyAddress using either FretrackAddressID or exact tokens in FretrackAddressIDs. Header CompanyAddressID is restricted to the mapped customer. Pickup/delivery addresses are scoped to the organization because their owners may differ from the customer.

For quote 227759, rollback-only database verification resolved CargoMintContactID=2670, CargoMintSalesQuoteCurrencyID=1 (INR), and CargoMintCompanyAddressID=10442. The source header currency code was blank, so the original code-only join could not resolve it; the source currency master now supplies that mapping without modifying source fields.

Remaining NULLs must be interpreted by field: generated quotation/revision/file IDs require the corresponding actual record and explicit migration linkage; NULL source Incoterm, commodity currency, pickup/delivery, old quote, modification/deletion users produce no new mapping. TypeOfMoveID=0 is not treated as a target ID. OfficeID is not assumed equivalent to OrgId; an office-to-target mapping must be defined before populating CargoMintOfficeID.


## Charge-only import (default)

Requests default to chargesOnly=true. This now returns the compact SalesQuoteChargesResponse with top-level service context and chargeLines[], a flat list of combined Buy/Sell inputs. The nested quotation and tables are available only when chargesOnly=false. Review messages are limited to salesQuotationLineItems fields; commodity, header, service status, option status and cargo warnings are excluded. Set chargesOnly=false only for the earlier full-quotation prefill behavior.

The CargoMint editor's Fretrack dialog selects an existing service and rate option. If only one is available it is selected automatically. Only new charge lines are appended to that option. No service or option is created and no header, customer, commodity, service status, option status, vendor/carrier, package or container values are overwritten. Existing line items are retained; new imported line IDs are unset and references point to the selected target quotation/option. Amounts are recalculated using the current quotation's financial context. Save through the normal quotation action to persist the newly patched lines.

Unit/tax/currency/charge-item mapping warnings still apply to imported lines and must be reviewed before saving. The existing organization-18 and service-access checks remain in place. The staging sync and mapping transaction is unchanged; the import does not directly save actual quotation lines.


Charge-only verification: RepoDbApi built successfully; Angular template/type compilation passed. Nine checks executing the component import/append methods verified selected-option targeting, preservation of header/status/cargo and existing rows, new line IDs, warning filtering, busy-state handling, removed-target handling and service access. Seven service/serialization checks verified default charge-only scope, flat lines, warning filtering, full-prefill opt-in and existing sync metadata. No live browser or database writes were performed for this change.


## Response fields matching the four CargoMint tables (chargesOnly=false)

The API now includes every column supplied for SalesQuotations (41), SalesQuotationServiceDetails (33), SalesQuotationRateOptions (23) and SalesQuotationLineItems (46). JSON uses the same camelCase naming as the CargoMint frontend: QuotationId becomes quotationId, SellRate becomes sellRate, and orgId stays orgId.

Two compatible views are returned:

```text
response.quotation                              existing nested form DTO
  salesQuotationServiceDetails[]
    salesQuotationRateOptions[]
      salesQuotationLineItems[]

response.tables.salesQuotations[]                exactly the 41 header columns
response.tables.salesQuotationServiceDetails[]   exactly the 33 service columns
response.tables.salesQuotationRateOptions[]      exactly the 23 option columns
response.tables.salesQuotationLineItems[]        exactly the 46 line columns
response.tables.rowLinks[]                       parent/child array index metadata

response.chargeLines[]                          existing flat charge DTOs
```

Table arrays exclude dropdown labels, review messages and nested collections; those remain in quotation. Audit fields and local-currency line/service/header totals are now included. Target-specific fields without a source mapping, such as HsnCode, BuyingRateId, RateRequestId, presentation lock and generated IDs, are explicitly NULL rather than invented.

New quotations have NULL target primary/foreign IDs until the normal CargoMint save creates them. rowLinks uses zero-based quotationRowIndex, serviceRowIndex, rateOptionRowIndex and lineItemRowIndices to preserve associations in the flat arrays without fabricating database IDs. The nested quotation view also preserves these associations directly.

Charge-only behavior is unchanged: use response.chargeLines or response.tables.salesQuotationLineItems to append new lines to the selected existing option; do not patch response.tables.salesQuotations over the current header. Set the current target quotation/option references when constructing new line form controls, and clear imported line IDs. The provided CargoMint import dialog already performs this binding and recalculates using the current quotation context. Local amounts and totals in the response are previews in the imported source quotation's mapped currency context, not an instruction to overwrite existing header financials.

SalesQuote-Table-Response.example.json contains a generated contract example with illustrative test IDs, not a live database response. The shape and exact column coverage are checked against the user-supplied target schema.


## Compact charge response — current default

```json
{
  "salesQuoteNumber": "SQCOU-M252600034",
  "orgId": 18,
  "importScope": "CHARGES_ONLY",
  "serviceTypeId": 9,
  "serviceLabel": "Courier",
  "transportModeId": 5,
  "transportMode": "SURFACE",
  "transportDirectionId": 1,
  "transportDirection": "EXPORT",
  "chargeLines": [
    {
      "orgId": 18,
      "chargeItemId": 82,
      "chargeDescription": "COURIER CHARGES",
      "sellCurrencyId": 2,
      "sellExRate": 94,
      "sellRate": 794,
      "sellQuantity": 1,
      "buyCurrencyId": 1,
      "buyExRate": 1,
      "buyRate": 54107,
      "buyQuantity": 1,
      "buyTaxPercent": 18
    }
  ]
}
```

This is the requested shape; IDs in a live response come from mapped staging/master values, not constants. Optional charge inputs such as applyPerId, tax IDs, sellTaxPercent, billing-party IDs, sortOrder and HsnCode are included only when populated. No generated line/quotation IDs, audit columns, calculated totals, review dictionaries or duplicate table snapshots are included in the compact output. The current quote editor already consumes the flat lines, preserves target header/service/option values and recalculates totals in the target quote context.

Top-level service IDs enable the editor's existing source-service access check without requiring a nested quotation graph. Several source details with the same service context can be flattened; different service contexts cannot be represented by one compact header and return 409 with instructions to request the full response. The current Fretrack mapper uses the quotation header's common service context for each detail.

Request: POST /api/fretrack-migration/single-salesquote with salesQuoteNumber, orgId, dryRun and chargesOnly=true. Set chargesOnly=false only when the full quotation/table response is needed. Swagger documents the default compact success response.


Compact contract example: SalesQuote-Charges-Response.example.json, generated from the requested sample values by the response serializer. Backend build and Angular compiler checks passed. Eleven controller/serialization checks verified the compact shape, optional input preservation, full-mode opt-in and service-context handling. Eleven UI method checks verified direct flat-response import, target selection, form preservation and service access. No live database or browser writes were performed for this response-format change.


## CargoMint service type names

Service labels use the supplied CargoMint master names: Air, FCL, LCL, Trucking, Rail, Warehouse, Custom Clearance, Courier, Breakbulk, Documentation. FCL EXPORT/FCL IMPORT normalize to FCL; LCL EXPORT/LCL IMPORT normalize to LCL. Matching is case-insensitive and tolerates repeated whitespace. Transport mode and direction remain separate fields and are not changed by label normalization. For example, Courier stays Courier while transportMode=SURFACE and transportDirection=EXPORT.

The lookup reads active dbo.ServiceTypes.ServiceType rows for the target organization/shared scope. ServiceTypeId is taken from a validated mapping to an active master row, or an unambiguous exact master-name match; no numeric IDs are assigned from the order of the name list. Exact Fretrack service/mode mappings remain preferred; when absent, the base CargoMint name is used as a fallback lookup key. Missing or ambiguous matches remain unresolved rather than choosing an arbitrary ID.


Service-name verification: the project built successfully, and 31 normalization/compact-response checks passed. Read-only lookup against the configured organization-18 development master confirmed active Air, FCL, LCL, Trucking, Warehouse, Custom Clearance, Courier and Documentation rows. Rail and Breakbulk exist with IsActive=false and IsDeleted=false; their names are recognized but inactive IDs are not selected for new imports. No master records or staging values were changed by this normalization task.


Compact route fields: cargoMintPOLID and cargoMintPODID are returned from the mapped staging CargoMintPOLID/CargoMintPODID via the service DTO, not from raw Fretrack POLID/PODID. Unresolved values remain NULL. Multiple source details must share the same service and mapped route to fit one compact response; otherwise request chargesOnly=false. Existing charges-only UI import does not overwrite the selected target service route.
