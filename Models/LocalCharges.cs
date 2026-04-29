namespace GeneratedModels
{
    public class LocalCharges
    {
        public int LocalChargeID { get; set; }
        public string LocalChargeName { get; set; }
        public string LocalChargeDescription { get; set; }
        public string ModeOfTransport { get; set; }
        public string TransportDirection { get; set; }
        public int? POLID { get; set; }
        public string POL { get; set; }
        public int? PODID { get; set; }
        public string POD { get; set; }
        public int? carrierID { get; set; }
        public string carrierName { get; set; }
        public int? TerminalID { get; set; }
        public string TerminalName { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public string ApplyPer { get; set; }
        public string ContainerType { get; set; }
        public decimal? RATE { get; set; }
        public string Currency { get; set; }
        public int? TaxID { get; set; }
        public string TaxName { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal? Minimum { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public int? chargeItemID { get; set; }
    }
}
