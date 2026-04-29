namespace GeneratedModels
{
    public class TerminalMaster
    {
        public int TerminalID { get; set; }
        public int LocationID { get; set; }
        public string TerminalName { get; set; }
        public bool isActive { get; set; }
        public bool isDeleted { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
