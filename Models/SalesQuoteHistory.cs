namespace GeneratedModels
{
    public class SalesQuoteHistory
    {
        public int SalesQuoteHistoryID { get; set; }
        public int SalesQuoteID { get; set; }
        public bool isDeleted { get; set; }
        public int? RevisionID { get; set; }
        public string SalesQuoteStatus { get; set; }
        public string SalesQuoteStatusRemarks { get; set; }
        public int? AssignedBy { get; set; }
        public int? AssignedTo { get; set; }
        public int? TaskID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
