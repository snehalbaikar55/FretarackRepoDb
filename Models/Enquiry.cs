namespace GeneratedModels
{
    public class Enquiry
    {
        public int ServiceTypeID { get; set; }
        public int EnquiryID { get; set; }
        public DateTime EnquiryDate { get; set; }
        public string EnquirySubject { get; set; }
        public int? POLID { get; set; }
        public string POL { get; set; }
        public int? PODID { get; set; }
        public string POD { get; set; }
        public int? CommodityID { get; set; }
        public string Commodity { get; set; }
        public int? ContainerTypeID { get; set; }
        public int? ContainerCount { get; set; }
        public int? PackageTypeID { get; set; }
        public int? PackageCount { get; set; }
        public int? Length { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public decimal? TotalCBM { get; set; }
        public decimal? TotalGrossWeight { get; set; }
        public string WeightUnit { get; set; }
        public string EnquirySource { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? AssignedTo { get; set; }
        public string EnquiryStatus { get; set; }
    }
}
