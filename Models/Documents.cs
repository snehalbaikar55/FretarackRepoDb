namespace GeneratedModels
{
    public class Documents
    {
        public int DocumentID { get; set; }
        public string DocumentName { get; set; }
        public string DocumentFileType { get; set; }
        public string DocumentLocalLink { get; set; }
        public string DocumentFTPLink { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
