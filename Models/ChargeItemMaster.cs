namespace GeneratedModels
{
    public class ChargeItemMaster
    {
        public bool isExpense { get; set; }
        public int ChargeItemID { get; set; }
        public string ChargeItemCode { get; set; }
        public string ChargeItemName { get; set; }
        public string OperationsGroup { get; set; }
        public int? TaxID { get; set; }
        public string TaxName { get; set; }
        public decimal? TaxPercent { get; set; }
        public string ChargeDescription { get; set; }
        public string TransportationMethod { get; set; }
        public string TransportationDirection { get; set; }
        public int? IncomeAccountHeadID { get; set; }
        public string IncomeAccountHead { get; set; }
        public int? ExpenseAccountHeadID { get; set; }
        public string ExpenseAccountHead { get; set; }
        public bool? AllowNegative { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public bool? isActive { get; set; }
    }
}
