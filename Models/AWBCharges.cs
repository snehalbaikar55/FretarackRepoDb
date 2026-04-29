namespace GeneratedModels
{
    public class AWBCharges
    {
        public int AWBChargeID { get; set; }
        public int AWBID { get; set; }
        public int CargoID { get; set; }
        public int AWBChargeTypeID { get; set; }
        public bool isDeleted { get; set; }
        public string DueType { get; set; }
        public string ApplyPer { get; set; }
        public decimal? ChargeAmount { get; set; }
        public int? SortOrder { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
