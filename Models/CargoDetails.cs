namespace GeneratedModels
{
    public class CargoDetails
    {
        public int CargoDetailID { get; set; }
        public int? CargoID { get; set; }
        public string NominationType { get; set; }
        public int? AgentID { get; set; }
        public int? AgentAddressID { get; set; }
        public string AgentName { get; set; }
        public DateTime? JobDate { get; set; }
        public int? HandledBy { get; set; }
        public decimal? TotalCBM { get; set; }
        public decimal? TotalGrossWeight { get; set; }
        public decimal? TotalChargeableWeight { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public bool? isDeleted { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
