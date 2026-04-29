namespace GeneratedModels
{
    public class SalesQuoteCommodities
    {
        public int SalesQuoteCommodityID { get; set; }
        public int SalesQuoteID { get; set; }
        public bool isDeleted { get; set; }
        public int? CommodityID { get; set; }
        public string CommodityDescription { get; set; }
        public int? PackageTypeID { get; set; }
        public int? Quantity { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public int? SizeUnitID { get; set; }
        public decimal? GrossWeight { get; set; }
        public int? WeightUnitID { get; set; }
        public decimal? Volume { get; set; }
        public int? VolumeUnitID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
