namespace GeneratedModels
{
    public class InvoiceESync
    {
        public int SyncID { get; set; }
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; }
        public string AckNo { get; set; }
        public DateTime AckDt { get; set; }
        public string IrnNo { get; set; }
        public string SignedInvoice { get; set; }
        public string SignedQRCode { get; set; }
        public string SyncResponse { get; set; }
        public int? SyncedBy { get; set; }
        public DateTime? SyncDate { get; set; }
    }
}
