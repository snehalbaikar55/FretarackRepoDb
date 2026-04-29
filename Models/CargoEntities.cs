namespace GeneratedModels
{
    public class CargoEntities
    {
        public int CargoEntityID { get; set; }
        public int CargoID { get; set; }
        public int? CompanyID { get; set; }
        public int? EntityTypeID { get; set; }
        public string EntityName { get; set; }
        public int? EntityAddressID { get; set; }
        public int? EntityCountryID { get; set; }
        public string EntityCountry { get; set; }
        public int? EntityStateID { get; set; }
        public string EntityState { get; set; }
        public int? EntityCityID { get; set; }
        public string EntityCity { get; set; }
        public string EntityAddressLine1 { get; set; }
        public string EntityZipCode { get; set; }
        public string EntityDisplayText { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool? isPayor { get; set; }
        public bool? isDeleted { get; set; }
        public string? CompanyName { get; set; }
    }
}
