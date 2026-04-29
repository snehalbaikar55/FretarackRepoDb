namespace GeneratedModels
{
    public class UserMaster
    {
        public bool isActive { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string UserPassword { get; set; }
        public string Email { get; set; }
        public int? ContactID { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public int? LoginFailStreak { get; set; }
        public int? LoginFailTotal { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public int? PrimaryOfficeID { get; set; }
        public int? ProfileID { get; set; }
        public DateTime? DateLocked { get; set; }
        public int? ManagerID { get; set; }
        public string UserDisplayName { get; set; }
    }
}
