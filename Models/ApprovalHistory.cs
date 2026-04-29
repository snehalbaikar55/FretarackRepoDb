namespace GeneratedModels
{
    public class ApprovalHistory
    {
        public string ApprovalType { get; set; }
        public int ApprovalEntityID { get; set; }
        public string ApprovalEntityReference { get; set; }
        public int ApproverID { get; set; }
        public int RequestorID { get; set; }
        public DateTime CompletedDate { get; set; }
        public int ApprovalHistoryID { get; set; }
        public int ApprovalQueueID { get; set; }
        public bool isApproved { get; set; }
        public bool isRejected { get; set; }
        public string Remarks { get; set; }
        public int? FormID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public int? ApprovalLevel { get; set; }
        public string CurrentStatus { get; set; }
        public DateTime? RequestDate { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
