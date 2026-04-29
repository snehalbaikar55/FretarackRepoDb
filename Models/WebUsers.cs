namespace GeneratedModels
{
    public class WebUsers
    {
        public bool isDeleted { get; set; }
        public bool isLocked { get; set; }
        public string UserName { get; set; }
        public int WebUserID { get; set; }
        public int? LinkedDesktopUserID { get; set; }
        public string UserType { get; set; }
        public string UserPassword { get; set; }
        public string UserDisplayName { get; set; }
        public int? CompanyID { get; set; }
        public string CompanyDisplayName { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public string DateDeleted { get; set; }
        public int? LockedBy { get; set; }
        public DateTime? DateLocked { get; set; }
        public string Remarks { get; set; }
    }
}
