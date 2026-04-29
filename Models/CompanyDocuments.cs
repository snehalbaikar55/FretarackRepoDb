namespace GeneratedModels
{
    public class CompanyDocuments
    {
        public int DocumentID { get; set; }
        public int CompanyID { get; set; }
        public string DocumentType { get; set; }
        public string DocumentLink { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public bool isDeleted { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
