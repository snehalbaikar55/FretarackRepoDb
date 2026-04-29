namespace GeneratedModels
{
    public class CargoPackages
    {
        public int CargoPackID { get; set; }
        public int CargoID { get; set; }
        public int CargoPackTypeID { get; set; }
        public int? ContainerID { get; set; }
        public string CargoPackName { get; set; }
        public int? PackageCount { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public int? SizeID { get; set; }
        public decimal? NetWeight { get; set; }
        public decimal? GrossWeight { get; set; }
        public int? WeightUnitID { get; set; }
        public decimal? Volume { get; set; }
        public int? VolumeUnitID { get; set; }
        public string PackageDescription { get; set; }
        public string MarksAndNumbers { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string SBNo { get; set; }
        public DateTime? SBDate { get; set; }
        public bool? isPerPackage { get; set; }
        public decimal? WeightKGS { get; set; }
        public decimal? WeightLBS { get; set; }
        public decimal? VolumeCBM { get; set; }
        public decimal? VolumeFT3 { get; set; }
        public decimal? VolumeWeight { get; set; }
        public decimal? TotalNetWeight { get; set; }
        public decimal? TotalGrossWeight { get; set; }
        public decimal? TotalVolume { get; set; }
        public decimal? TotalVolumeWeight { get; set; }
        public int? ParentPackageID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
