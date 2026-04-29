namespace GeneratedModels
{
    public class Contacts
    {
        public int ContactID { get; set; }
        public int CompanyID { get; set; }
        public bool isDeleted { get; set; }
        public DateTime? DOB { get; set; }
        public int? ContactTypeID { get; set; }
        public string Salutation { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public string Phone { get; set; }
        public string Department { get; set; }
        public int? ReportsTo { get; set; }
        public int? AddressID { get; set; }
        public string Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
