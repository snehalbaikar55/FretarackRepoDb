namespace GeneratedModels
{
    public class LocationMaster
    {
        public int LocationID { get; set; }
        public string LocationType { get; set; }
        public string LocationCode { get; set; }
        public string LocationShortName { get; set; }
        public string LocationLongName { get; set; }
        public int CountryID { get; set; }
        public string CountryName { get; set; }
        public bool isDeleted { get; set; }
        public bool isActive { get; set; }
        public int? StateID { get; set; }
        public string StateName { get; set; }
        public int? CityID { get; set; }
        public string CityName { get; set; }
        public string ScheduleD { get; set; }
        public string ScheduleK { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
