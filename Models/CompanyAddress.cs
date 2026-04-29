namespace GeneratedModels
{
    public class CompanyAddress
    {
        public int CountryID { get; set; }
        public string Country { get; set; }
        public int CompanyAddressID { get; set; }
        public int CompanyID { get; set; }
        public int AddressTypeID { get; set; }
        public int CreatedBy { get; set; }
        public bool isDeleted { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string FAxnumber { get; set; }
        public string GSTnumber { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public string AddressTypeNick { get; set; }
        public int? ContactID { get; set; }
        public int? StateID { get; set; }
        public string StateName { get; set; }
        public int? CityID { get; set; }
        public string CityName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string Zipcode { get; set; }
        public int? RelatedPortID { get; set; }
        public int? RelatedAirportID { get; set; }
        public string FormattedAddress { get; set; }
    }
}
