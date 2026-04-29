namespace GeneratedModels
{
    public class CompanyBankAccounts
    {
        public int BankAccountID { get; set; }
        public int CompanyID { get; set; }
        public string BankName { get; set; }
        public bool isDeleted { get; set; }
        public string AccountNumber { get; set; }
        public int? CountryID { get; set; }
        public string Branch { get; set; }
        public string IFSCCode { get; set; }
        public string SWIFT { get; set; }
        public string IBAN { get; set; }
        public string BankContactName { get; set; }
        public string BankContactNumber { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
