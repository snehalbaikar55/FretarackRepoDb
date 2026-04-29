namespace GeneratedModels
{
    public class ProfileRights
    {
        public int ProfileRightID { get; set; }
        public int ProfileID { get; set; }
        public int FormID { get; set; }
        public bool? allowView { get; set; }
        public bool? allowRead { get; set; }
        public bool? allowCreate { get; set; }
        public bool? allowModify { get; set; }
        public bool? allowDelete { get; set; }
        public bool? isApprover { get; set; }
        public bool? ApprovalReq { get; set; }
        public int? ApprovalLevel { get; set; }
    }
}
