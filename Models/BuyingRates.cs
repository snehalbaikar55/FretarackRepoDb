namespace GeneratedModels
{
    public class BuyingRates
    {
        public int BuyingRateID { get; set; }
        public int? POLID { get; set; }
        public string POL { get; set; }
        public int? PODID { get; set; }
        public string POD { get; set; }
        public string ModeOfTransport { get; set; }
        public string TransportDirection { get; set; }
        public string ContainerType { get; set; }
        public string Terms { get; set; }
        public string TransitTime { get; set; }
        public int? POT1ID { get; set; }
        public string POT1 { get; set; }
        public int? POT2ID { get; set; }
        public string POT2 { get; set; }
        public int? POT3ID { get; set; }
        public string POT3 { get; set; }
        public string Frequency { get; set; }
        public DateTime? OfferDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Contact { get; set; }
        public string Email { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; }
        public int? TaxID { get; set; }
        public string TaxName { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public bool? isApproved { get; set; }
        public decimal? C20GP { get; set; }
        public decimal? C20HC { get; set; }
        public decimal? C40HC { get; set; }
        public decimal? C40GP { get; set; }
        public decimal? C20OT { get; set; }
        public decimal? C40OT { get; set; }
        public decimal? C20FT { get; set; }
        public decimal? C40FT { get; set; }
        public int? carrierID { get; set; }
        public string carrierName { get; set; }
        public decimal? LCLRate { get; set; }
        public decimal? Minimum { get; set; }
        public bool? isLCL { get; set; }
        public decimal? C20RF { get; set; }
        public decimal? C40RF { get; set; }
        public decimal? LCL { get; set; }
        public decimal? bulk { get; set; }
        public string Commodity { get; set; }
        public string Notes { get; set; }
    }
}
