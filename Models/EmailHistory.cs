namespace GeneratedModels
{
    public class EmailHistory
    {
        public int EmailHistoryID { get; set; }
        public string EmailFrom { get; set; }
        public bool isHTML { get; set; }
        public bool isSent { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBodyHTML { get; set; }
        public string EmailBodyText { get; set; }
        public DateTime? SentDate { get; set; }
        public int? AttemptCount { get; set; }
        public string SendResponse { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public string EmailType { get; set; }
        public int? EmailReferenceID { get; set; }
        public string EmailReference { get; set; }
        public string EmailTo { get; set; }
        public string EmailCC { get; set; }
        public string EmailBCC { get; set; }
    }
}
