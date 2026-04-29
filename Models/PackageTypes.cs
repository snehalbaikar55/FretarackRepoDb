namespace GeneratedModels
{
    public class PackageTypes
    {
        public bool isContainer { get; set; }
        public bool isOcean { get; set; }
        public bool isAir { get; set; }
        public bool isSurface { get; set; }
        public bool isActive { get; set; }
        public int PackageTypeID { get; set; }
        public int PackageGroupID { get; set; }
        public string PackageTypeName { get; set; }
        public bool isDeleted { get; set; }
        public string PackageTypeShortName { get; set; }
        public string PackageCode { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public string ContainerISOCode { get; set; }
        public decimal? DefaultLength { get; set; }
        public decimal? DefaultWidth { get; set; }
        public decimal? DefaultHeight { get; set; }
        public int? DimensionsUnitID { get; set; }
        public decimal? Weight { get; set; }
        public decimal? MaxWeight { get; set; }
        public int? WeightUnitID { get; set; }
    }
}
