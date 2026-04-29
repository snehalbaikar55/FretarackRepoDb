namespace GeneratedModels
{
    public class LIVApprovalTasks
    {
        public int ApprovalTaskID { get; set; }
        public int RequestorID { get; set; }
        public int LIVRequestId { get; set; }
        public int ApproverId { get; set; }
        public int Level { get; set; }
        public string LevelStatus { get; set; }
        public int IsDeleted { get; set; }
        public string ApprovalSource { get; set; }
        public string Note { get; set; }
        public int? UserId { get; set; }
        public string ApprovalFilename { get; set; }
        public string Token { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? RejectedDate { get; set; }
        public string RejectReason { get; set; }
    }
}
