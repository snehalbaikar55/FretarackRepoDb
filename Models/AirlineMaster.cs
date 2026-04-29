namespace GeneratedModels
{
    public class AirlineMaster
    {
        public int AirlineID { get; set; }
        public bool isActive { get; set; }
        public int CountryID { get; set; }
        public bool isDeleted { get; set; }
        public string N3Code { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public string AirlineName { get; set; }
        public string A2Code { get; set; }
        public string A3Code { get; set; }
    }
}
