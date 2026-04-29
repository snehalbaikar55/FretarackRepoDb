namespace GeneratedModels
{
    public class CampaignTargetList
    {
        public int CampaignTargetID { get; set; }
        public bool isDeleted { get; set; }
        public int? CampaignID { get; set; }
        public int? LeadID { get; set; }
        public bool? isSent { get; set; }
        public DateTime? SentDate { get; set; }
        public bool? isResponded { get; set; }
        public DateTime? RespondedDate { get; set; }
        public string ResponseLog { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
