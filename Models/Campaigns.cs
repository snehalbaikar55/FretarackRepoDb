namespace GeneratedModels
{
    public class Campaigns
    {
        public int CampaignOwnerID { get; set; }
        public int CampaignID { get; set; }
        public int CampaignTypeID { get; set; }
        public string CampaignDescription { get; set; }
        public string CampaignName { get; set; }
        public bool? isActive { get; set; }
        public int? ParentCampaignID { get; set; }
        public string CampaignStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ImpressionsTargetted { get; set; }
        public int? ImpressionsRecieved { get; set; }
        public decimal? ExpectedResponse { get; set; }
        public decimal? ActualResponse { get; set; }
        public decimal? BudgetedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public decimal? ExpectedRevenue { get; set; }
        public int? CurrencyID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
