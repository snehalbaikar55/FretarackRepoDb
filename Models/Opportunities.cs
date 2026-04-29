namespace GeneratedModels
{
    public class Opportunities
    {
        public int OpportunityID { get; set; }
        public string OpportunityName { get; set; }
        public bool isDeleted { get; set; }
        public string ApprovalStatus { get; set; }
        public int? AccountID { get; set; }
        public string AccountName { get; set; }
        public string OpportunityType { get; set; }
        public int? OpportunityOwnerID { get; set; }
        public DateTime? DateClose { get; set; }
        public int? OpportunityStageID { get; set; }
        public byte? ProbabilityPercent { get; set; }
        public decimal? Amount { get; set; }
        public int? CurrencyID { get; set; }
        public string NextStep { get; set; }
        public string Description { get; set; }
        public int? PrimaryCampaignID { get; set; }
        public int? LeadSourceID { get; set; }
        public int? AssociatedLeadID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
