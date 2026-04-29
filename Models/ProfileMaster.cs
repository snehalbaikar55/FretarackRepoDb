namespace GeneratedModels
{
    public class ProfileMaster
    {
        public int ProfileID { get; set; }
        public string ProfileName { get; set; }
        public string AssociatedDesignation { get; set; }
        public bool isDeleted { get; set; }
        public int? OfficeID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
