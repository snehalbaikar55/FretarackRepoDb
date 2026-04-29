namespace GeneratedModels
{
    public class LivDocuments
    {
        public int Id { get; set; }
        public string DocumentName { get; set; }
        public string DocumentPath { get; set; }
        public DateTime? UploadedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public int? DeletedBy { get; set; }
        public int? UploadedBy { get; set; }
        public int? LivRequestId { get; set; }
        public string Source { get; set; }
        public int? SourceId { get; set; }
    }
}
