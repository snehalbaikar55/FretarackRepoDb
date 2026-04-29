namespace GeneratedModels
{
    public class ChargeGroupMaster
    {
        public int ChargeGroupID { get; set; }
        public string ChargeGroupCode { get; set; }
        public string ChargeGroupName { get; set; }
        public string ChargeGroupDescription { get; set; }
        public int? ParentChargeGroupID { get; set; }
        public bool? isExpense { get; set; }
        public bool? isIncome { get; set; }
        public bool? isAir { get; set; }
        public bool? isOcean { get; set; }
        public bool? isSurface { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
