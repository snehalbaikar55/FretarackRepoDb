namespace GeneratedModels
{
    public class vw_InvoiceList
    {
        public int InvoiceID { get; set; }
        public int CargoID { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public bool isSentToParty { get; set; }
        public bool isLocked { get; set; }
        public int? LockedBy { get; set; }
        public DateTime? DateLocked { get; set; }
        public int? SentBy { get; set; }
        public DateTime? SentDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public string InvoiceApprovalStatus { get; set; }
        public int? CostSheetID { get; set; }
        public int? OfficeID { get; set; }
        public string CompanyName { get; set; }
        public string TransportDirection { get; set; }
        public string ModeOfTransport { get; set; }
        public string InvoiceType { get; set; }
        public int? PayingPartyID { get; set; }
        public string PayingParty { get; set; }
        public int? PayingPartyAddressID { get; set; }
        public string PayingPartyAddress { get; set; }
        public int? CurrencyID { get; set; }
        public string CurrencyCode { get; set; }
        public int? ExchangeRateID { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string JobNumber { get; set; }
        public string VesselVoyage { get; set; }
        public string Cycle { get; set; }
        public string CargoType { get; set; }
        public string FreightStatus { get; set; }
        public string MBL_MAWB { get; set; }
        public string POL { get; set; }
        public string FinalDestination { get; set; }
        public string CreditDays { get; set; }
        public string ShipperInvoiceDetails { get; set; }
        public string HBL_HAWB { get; set; }
        public string FlightDetails { get; set; }
        public string Notes1 { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public decimal? TaxAmount { get; set; }
        public string InvoiceAmountWords { get; set; }
        public decimal? InvoiceAmountLocalCurrency { get; set; }
        public decimal? TaxAmountLocalCurrency { get; set; }
        public decimal? ServiceTax { get; set; }
        public decimal? EducationCess { get; set; }
        public decimal? SHECess { get; set; }
        public decimal? NonTaxableAmount { get; set; }
        public decimal? TaxableAmount { get; set; }
        public int? LocalCurrencyID { get; set; }
    }
}
