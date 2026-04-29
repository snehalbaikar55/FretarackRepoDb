namespace GeneratedModels
{
    public class Leads
    {
        public int LeadID { get; set; }
        public string Salutation { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Title { get; set; }
        public string Email { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public int? CountryID { get; set; }
        public int? StateID { get; set; }
        public int? CityID { get; set; }
        public string CityName { get; set; }
        public string Street { get; set; }
        public string ZipCode { get; set; }
        public int? CompanyID { get; set; }
        public string CompanyName { get; set; }
        public int? IndustryID { get; set; }
        public int? EmployeeCount { get; set; }
        public int? LeadSourceID { get; set; }
        public int? LeadStatusID { get; set; }
        public int? LeadOwnerID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public bool? isCallingAllowed { get; set; }
        public string Notes { get; set; }
        public DateTime? Followup1 { get; set; }
        public DateTime? Followup2 { get; set; }
        public DateTime? Followup3 { get; set; }
        public DateTime? ProfileSentOn { get; set; }
        public DateTime? Meeting1 { get; set; }
        public DateTime? Meeting2 { get; set; }
        public DateTime? Meeting3 { get; set; }
        public DateTime? EnquiryReceivedOn { get; set; }
        public string ShipmentType { get; set; }
        public string Sector { get; set; }
        public DateTime? QuoteSentOn { get; set; }
        public DateTime? RevQuoteSentOn { get; set; }
        public bool? Secured { get; set; }
        public int? AttendedBy { get; set; }
        public string ResonNotSecured { get; set; }
    }
}
