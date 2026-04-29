namespace GeneratedModels
{
    public class SalesQuoteServices
    {
        public int DestinationLocationID { get; set; }
        public int SalesQuoteServiceID { get; set; }
        public int SalesQuoteID { get; set; }
        public int OriginLocationID { get; set; }
        public bool isDeleted { get; set; }
        public string OriginPoint { get; set; }
        public byte? SalesQuoteServiceOrder { get; set; }
        public int? ServiceTypeID { get; set; }
        public string DestinationPoint { get; set; }
        public int? TransshipmentPoint1 { get; set; }
        public int? TransshipmentPoint2 { get; set; }
        public int? TransshipmentPoint3 { get; set; }
        public int? TransitTime { get; set; }
        public int? FrequencyID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
