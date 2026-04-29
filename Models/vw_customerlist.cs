namespace GeneratedModels
{
    public class vw_customerlist
    {
        public int ParentCompanyID { get; set; }
        public int OfficeID { get; set; }
        public int CompanyID { get; set; }
        public string CompanyName { get; set; }
        public int CompanyTypeID { get; set; }
        public string CompanyTypeName { get; set; }
        public string CompanyCode { get; set; }
        public string CompanyApprovalStatus { get; set; }
        public int? ownerID { get; set; }
        public bool? CafReceived { get; set; }
        public DateTime? CafReceivedDate { get; set; }
        public string CreditDays { get; set; }
        public string MepzCode { get; set; }
        public string PANnumber { get; set; }
        public string CityName { get; set; }
        public string AddressTypeNick { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
