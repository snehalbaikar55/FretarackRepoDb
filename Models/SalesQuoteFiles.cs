namespace GeneratedModels
{
    public class SalesQuoteFiles
    {
        public int SalesQuoteFileID { get; set; }
        public int SalesQuoteID { get; set; }
        public bool isDeleted { get; set; }
        public int? RevisionID { get; set; }
        public string FilePath { get; set; }
        public string EmailTo { get; set; }
        public string EmailCC { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
