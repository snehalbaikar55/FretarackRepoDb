namespace GeneratedModels
{
    public class CargoDocuments
    {
        public int CargoDocumentID { get; set; }
        public int? DocumentTypeID { get; set; }
        public string DocumentType { get; set; }
        public string DocumentName { get; set; }
        public string DocumentFileType { get; set; }
        public string Remarks { get; set; }
        public string FTPLink { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
        public int? CargoID { get; set; }
        public string? CreatedByName { get; set; }
    }
}
