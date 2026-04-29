namespace GeneratedModels
{
    public class CompanyPhones
    {
        public int PhoneID { get; set; }
        public int CountryID { get; set; }
        public int PhoneTypeID { get; set; }
        public int CompanyID { get; set; }
        public int? ContactID { get; set; }
        public string PhoneReference { get; set; }
        public string CountryName { get; set; }
        public string STDCode { get; set; }
        public string PhoneNumber { get; set; }
        public string Extension { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
