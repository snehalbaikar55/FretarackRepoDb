using GeneratedModels;
using RepoDbApi.Contracts;
using RepoDbApi.Repositories;

namespace RepoDbApi.Services;

public static class SalesQuotePrefillBuilder
{
    public static SalesQuotePrefillData Build(MappedSalesQuoteHeader header,
        List<MappedSalesQuoteDetail> details, List<SalesQuoteNomDetails> nominations,
        List<MappedSalesQuoteCharge> charges, SalesQuotePrefillReferences references, int orgId)
    {
        if (header.CargoMintSalesQuoteID.HasValue)
            throw new SalesQuoteSyncConflictException("This source quote is already linked to an actual CargoMint quotation. Load that quotation instead of creating a new prefill.");
        var result = new SalesQuotePrefillData
        {
            OrgId = orgId, QuotationNo = header.SalesQuoteNumber,
            QuotationDate = header.SalesQuoteDate, ReceivedDate = header.EnqReceivedDate, ValidityDate = header.ExpiryDate,
            CustomerId = Positive(header.CargoMintCompanyID), CompanyName = header.CompanyDisplayName,
            ContactId = Positive(header.CargoMintContactID), ContactName = header.ContactDisplayName,
            SalesPersonId = Positive(header.CargoMintSalesPersonID), SalesPersonName = header.SalesPersonDisplayName,
            CreatedBy = Positive(header.CargoMintCreatedBy), PreparedByName = header.PreparedByDislayName,
            CreatedDate = header.DateCreated, UpdatedBy = Positive(header.CargoMintModifiedBy), UpdatedDate = header.DateModified,
            IsDeleted = header.isDeleted, DeletedBy = Positive(header.CargoMintDeletedBy), DeletedDate = header.DateDeleted,
            CurrencyId = Positive(header.CargoMintSalesQuoteCurrencyID), LocalCurrencyId = Positive(references.LocalCurrencyId),
            IncoTermId = Positive(header.CargoMintIncoTermID), CommodityId = references.CommodityId, CommodityName = header.Commodity,
            TermsConditions = header.TermsConditions, IsHazCargo = header.isHazardous,
            ShowTax = header.showTax ?? true, ShowCarrier = header.showCarrier ?? false
        };
        void Missing(string path, string reason)
        {
            if (!result.PrefillMissingFields.TryGetValue(path, out var messages))
                result.PrefillMissingFields[path] = messages = new();
            if (!messages.Contains(reason)) messages.Add(reason);
        }
        void Require(int? id, string path) { if (!Positive(id).HasValue) Missing(path, "CargoMint master mapping is missing or ambiguous."); }
        string? Company(int? id) => references.Companies.FirstOrDefault(x => x.Id == id)?.Name;
        int? ScopedCompany(int? id) => Positive(id).HasValue && references.Companies.Any(x => x.Id == id) ? id : null;
        int? Currency(int? id) => Positive(id).HasValue && references.Currencies.Any(x => x.CurrencyId == id) ? id : null;
        result.CustomerId = ScopedCompany(result.CustomerId);
        result.CurrencyId = Currency(result.CurrencyId);
        result.CurrencyCode = references.Currencies.FirstOrDefault(x => x.CurrencyId == result.CurrencyId)?.CurrencyCode;
        var activeCharges = charges.Where(x => !x.isDeleted).ToList();
        var types = nominations.Where(x => x.isDeleted != true).Select(x => x.NominationType?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (types.Count == 1) result.NominationType = types[0];
        if (types.Count > 1) Missing("nominationType", "Multiple source nomination types require selection.");
        Require(result.CustomerId, "customerId"); Require(result.ContactId, "contactId");
        Require(result.SalesPersonId, "salesPersonId"); Require(result.CurrencyId, "currencyId");
        Require(result.LocalCurrencyId, "localCurrencyId");
        if (!result.QuotationDate.HasValue) Missing("quotationDate", "Source quotation date is missing.");
        if (!string.IsNullOrWhiteSpace(header.Commodity)) Require(result.CommodityId, "commodityId");
        if (Positive(header.IncoTermID).HasValue) Require(result.IncoTermId, "incoTermId");

        if (result.CurrencyId.HasValue && result.CurrencyId == result.LocalCurrencyId) result.ExRate = 1;
        else if (result.CurrencyId.HasValue)
        {
            var rates = activeCharges.Where(x => x.CargoMintCurrencyID == result.CurrencyId && x.ExRate > 0)
                .Select(x => x.ExRate!.Value).Distinct().ToList();
            if (rates.Count == 1) result.ExRate = rates[0];
        }
        if (!result.ExRate.HasValue) Missing("exRate", "Choose the header-to-local exchange rate before saving; no rate was invented.");
        decimal? headerToLocal = result.CurrencyId.HasValue && result.LocalCurrencyId.HasValue
            ? (result.CurrencyId == result.LocalCurrencyId ? 1m : result.ExRate) : null;
        if (activeCharges.Any(x => !details.Any(d => d.SalesQuoteDetailID == x.SQDetailRowID)))
            throw new SalesQuoteSyncConflictException("An active charge has no matching staged quotation detail.");
        if (activeCharges.Any(x => Side(x) is not ("INCOME" or "EXPENSE")))
            throw new SalesQuoteSyncConflictException("A charge has an unsupported IncomeExpense value.");

        foreach (var detail in details.OrderBy(x => x.SortOrder).ThenBy(x => x.SalesQuoteDetailID))
        {
            var index = result.SalesQuotationServiceDetails.Count;
            var path = $"salesQuotationServiceDetails[{index}]";
            var service = new SalesQuotePrefillService
            {
                OrgId = orgId, ServiceTypeId = references.ServiceTypeId,
                ServiceLabel = SalesQuoteServiceNames.Label(references.ServiceTypeName ?? header.SalesQuoteType, header.Modeoftransport),
                TransportModeId = references.ModeId, TransportMode = header.Modeoftransport,
                TransportDirectionId = references.DirectionId, TransportDirection = header.Direction,
                PolId = Positive(detail.CargoMintPOLID), Pol = detail.POL,
                PodId = Positive(detail.CargoMintPODID), Pod = detail.POD,
                ServiceNotes = detail.Notes?.Length <= 255 ? detail.Notes : null, IsPrimary = index == 0,
                CurrencyId = result.CurrencyId, ExRate = result.ExRate, LocalCurrencyId = result.LocalCurrencyId,
                CreatedBy = result.CreatedBy, CreatedDate = result.CreatedDate, UpdatedBy = result.UpdatedBy, UpdatedDate = result.UpdatedDate
            };
            Require(service.ServiceTypeId, path + ".serviceTypeId");
            Require(service.TransportModeId, path + ".transportModeId");
            Require(service.TransportDirectionId, path + ".transportDirectionId");
            if (Positive(detail.POLID).HasValue) Require(service.PolId, path + ".polId");
            if (Positive(detail.PODID).HasValue) Require(service.PodId, path + ".podId");
            Missing(path + ".serviceStatus", "Select a CargoMint service status.");
            var option = new SalesQuotePrefillOption
            {
                OrgId = orgId, CarrierId = Positive(detail.CargoMintCarrierID), CarrierName = detail.Carrier,
                OptionNotes = detail.Notes,
                CurrencyId = result.CurrencyId, ExRate = result.ExRate, LocalCurrencyId = result.LocalCurrencyId,
                CreatedBy = result.CreatedBy, CreatedDate = result.CreatedDate, UpdatedBy = result.UpdatedBy, UpdatedDate = result.UpdatedDate
            };
            if (Positive(detail.CarrierID).HasValue) Require(option.CarrierId, path + ".salesQuotationRateOptions[0].carrierId");
            Missing(path + ".salesQuotationRateOptions[0].optionStatus", "Select a CargoMint rate-option status.");
            var groups = activeCharges.Where(x => x.SQDetailRowID == detail.SalesQuoteDetailID)
                .GroupBy(x => (x.ChargeItemID, x.LineNumber, Unit: Normalize(x.ApplyPer)))
                .OrderBy(x => x.Key.LineNumber).ThenBy(x => x.Key.ChargeItemID);
            foreach (var group in groups)
            {
                var sells = group.Where(x => Side(x) == "INCOME").ToList();
                var buys = group.Where(x => Side(x) == "EXPENSE").ToList();
                if (sells.Count > 1 || buys.Count > 1)
                    throw new SalesQuoteSyncConflictException("Ambiguous buy/sell pairing: duplicate detail/item/line/unit/side.");
                var sell = sells.SingleOrDefault(); var buy = buys.SingleOrDefault();
                if (Positive(sell?.CargoMintChargeItemID).HasValue && Positive(buy?.CargoMintChargeItemID).HasValue
                    && sell!.CargoMintChargeItemID != buy!.CargoMintChargeItemID)
                    throw new SalesQuoteSyncConflictException("Paired charges map to different CargoMint charge items.");
                var source = sell ?? buy!;
                var unit = references.Units.Where(x => UnitCode(x.Name) == UnitCode(source.ApplyPer)).ToList();
                var line = new SalesQuotePrefillLine
                {
                    OrgId = orgId, ChargeItemId = Positive(sell?.CargoMintChargeItemID) ?? Positive(buy?.CargoMintChargeItemID),
                    ChargeDescription = source.ChargeDescription, SortOrder = source.LineNumber,
                    ApplyPerId = unit.Count == 1 ? unit[0].Id : null, ApplyPerName = source.ApplyPer,
                    SellCurrencyId = Currency(sell?.CargoMintCurrencyID), SellCurrencyCode = sell?.CurrencyCode,
                    SellExRate = sell?.ExRate, SellRate = Round(sell?.Rate, 2), SellQuantity = sell?.Quantity,
                    SellTaxPercent = Round(sell?.TaxPercent, 2), SellTaxId = TaxId(sell, references),
                    BuyCurrencyId = Currency(buy?.CargoMintCurrencyID), BuyCurrencyCode = buy?.CurrencyCode,
                    BuyExRate = buy?.ExRate, BuyRate = Round(buy?.Rate, 2), BuyQuantity = buy?.Quantity,
                    BuyTaxPercent = Round(buy?.TaxPercent, 2), BuyTaxId = TaxId(buy, references),
                    InvoiceToId = ScopedCompany(sell?.CargoMintPayingPartyID), BillToId = ScopedCompany(buy?.CargoMintPayingPartyID),
                    LocalCurrencyId = result.LocalCurrencyId,
                    CreatedBy = Positive(sell?.CargoMintCreatedBy) ?? Positive(buy?.CargoMintCreatedBy),
                    CreatedDate = sell?.DateCreated ?? buy?.DateCreated,
                    UpdatedBy = Positive(sell?.CargoMintModifiedBy) ?? Positive(buy?.CargoMintModifiedBy),
                    UpdatedDate = sell?.DateModified ?? buy?.DateModified
                };
                line.InvoiceToName = Company(line.InvoiceToId);
                line.BillToName = Company(line.BillToId);
                var linePath = path + $".salesQuotationRateOptions[0].salesQuotationLineItems[{option.SalesQuotationLineItems.Count}]";
                Require(line.ChargeItemId, linePath + ".chargeItemId"); Require(line.ApplyPerId, linePath + ".applyPerId");
                foreach (var (charge, side) in new[] { (sell, "sell"), (buy, "buy") })
                {
                    if (charge is null) continue;
                    Require(side == "sell" ? line.SellCurrencyId : line.BuyCurrencyId, linePath + $".{side}CurrencyId");
                    Require(side == "sell" ? line.InvoiceToId : line.BillToId, linePath + (side == "sell" ? ".invoiceToId" : ".billToId"));
                    if (charge.TaxPercent != 0) Require(TaxId(charge, references), linePath + $".{side}TaxId");
                    if (charge.Quantity is null || charge.Rate is null || charge.TaxPercent is null || !(charge.ExRate > 0))
                        Missing(linePath + $".{side}Amounts", "Quantity, rate, tax percent or a positive exchange rate is missing.");
                }
                try
                {
                    (line.SellSubtotal, line.SellTaxAmount, line.SellTotalAmount) = Amounts(sell, result.ExRate);
                    (line.BuySubtotal, line.BuyTaxAmount, line.BuyTotalAmount) = Amounts(buy, result.ExRate);
                    line.SellSubtotalLocal = Local(line.SellSubtotal, headerToLocal);
                    line.SellTaxAmountLocal = Local(line.SellTaxAmount, headerToLocal);
                    line.SellTotalAmountLocal = Local(line.SellTotalAmount, headerToLocal);
                    line.BuySubtotalLocal = Local(line.BuySubtotal, headerToLocal);
                    line.BuyTaxAmountLocal = Local(line.BuyTaxAmount, headerToLocal);
                    line.BuyTotalAmountLocal = Local(line.BuyTotalAmount, headerToLocal);
                }
                catch (OverflowException) { Missing(linePath, "Amounts exceed the supported calculation range."); }
                option.SalesQuotationLineItems.Add(line);
            }
            var vendors = option.SalesQuotationLineItems.Select(x => x.BillToId).Where(x => x.HasValue).Distinct().ToList();
            if (vendors.Count == 1) { option.VendorId = vendors[0]; option.VendorName = Company(option.VendorId); }
            option.ApprovedCostRate = Sum(option.SalesQuotationLineItems.Select(x => x.BuyTotalAmount));
            option.FinalSalesRate = Sum(option.SalesQuotationLineItems.Select(x => x.SellTotalAmount));
            service.ApprovedCostRate = option.ApprovedCostRate; service.FinalSalesRate = option.FinalSalesRate;
            service.ApprovedCostRateLocal = Local(service.ApprovedCostRate, headerToLocal);
            service.FinalSalesRateLocal = Local(service.FinalSalesRate, headerToLocal);
            service.SalesQuotationRateOptions.Add(option); result.SalesQuotationServiceDetails.Add(service);
        }
        if (details.Count == 0) Missing("salesQuotationServiceDetails", "No source services were supplied.");
        // No cargo rows are invented from header weight/volume alone.
        Missing("cargoDetails", "Package/container counts, types and dimensions were not present in the four source staging tables; review cargo requirements.");
        result.TotalApprovedCostRate = Sum(result.SalesQuotationServiceDetails.Select(x => x.ApprovedCostRate));
        result.TotalFinalSalesRate = Sum(result.SalesQuotationServiceDetails.Select(x => x.FinalSalesRate));
        result.TotalProfit = result.TotalFinalSalesRate - result.TotalApprovedCostRate;
        result.TotalProfitPercentage = result.TotalFinalSalesRate > 0 && result.TotalProfit.HasValue
            ? Round(result.TotalProfit / result.TotalFinalSalesRate * 100m, 2) : null;
        result.TotalApprovedCostRateLocal = Local(result.TotalApprovedCostRate, headerToLocal);
        result.TotalFinalSalesRateLocal = Local(result.TotalFinalSalesRate, headerToLocal);
        result.TotalProfitLocal = Local(result.TotalProfit, headerToLocal);
        return result;
    }

    private static int? Positive(int? value) => value > 0 ? value : null;
    private static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private static string Side(MappedSalesQuoteCharge value) => Normalize(value.IncomeExpense);
    private static decimal? Round(decimal? value, int scale) => value.HasValue ? decimal.Round(value.Value, scale, MidpointRounding.AwayFromZero) : null;
    private static decimal? Local(decimal? amount, decimal? rate) => rate > 0 ? Round(amount * rate, 4) : null;
    private static decimal? Sum(IEnumerable<decimal?> values)
    {
        var list = values.ToList();
        return list.All(x => x.HasValue) ? list.Sum(x => x!.Value) : null;
    }
    private static int? TaxId(MappedSalesQuoteCharge? charge, SalesQuotePrefillReferences refs)
    {
        if (charge is null) return null;
        if (Positive(charge.CargoMintTaxRateID).HasValue)
            return refs.Taxes.Any(x => x.TaxGroupId == charge.CargoMintTaxRateID && x.TotalRate.HasValue
                && Round(x.TotalRate, 2) == Round(charge.TaxPercent, 2)) ? charge.CargoMintTaxRateID : null;
        var candidates = refs.Taxes.Where(x => x.TotalRate.HasValue && Round(x.TotalRate, 2) == Round(charge.TaxPercent, 2)).ToList();
        return candidates.Count == 1 ? candidates[0].TaxGroupId : null;
    }
    private static (decimal? Subtotal, decimal? Tax, decimal? Total) Amounts(MappedSalesQuoteCharge? charge, decimal? headerRate)
    {
        if (charge is null) return (0, 0, 0);
        if (!(headerRate > 0) || !(charge.ExRate > 0) || charge.Rate is null || charge.Quantity is null || charge.TaxPercent is null)
            return (null, null, null);
        var raw = Round(charge.Rate, 2)!.Value * charge.Quantity.Value * charge.ExRate.Value / headerRate.Value;
        var subtotal = Round(raw, 2)!.Value;
        var tax = Round(raw * Round(charge.TaxPercent, 2)!.Value / 100m, 2)!.Value;
        return (subtotal, tax, subtotal + tax);
    }
    private static string UnitCode(string? value) => Normalize(value) switch
    {
        "KGS" or "KG" or "KILOGRAM" or "KILOGRAMS" => "KG",
        "SHPT" or "SHIPMENT" or "PER SHIPMENT" => "SHIPMENT",
        "CNTR" or "CONTAINER" => "CONTAINER",
        "PKG" or "PACKAGE" => "PACKAGE",
        "INV" or "INVOICE" => "INVOICE",
        "DOC" or "DOCUMENT" => "DOCUMENT",
        var other => other
    };
}
