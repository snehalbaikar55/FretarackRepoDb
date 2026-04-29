namespace GeneratedModels
{
    public class vw_Accounts_GST_Base
    {
        public DateTime invoicedate { get; set; }
        public string invoicenumber { get; set; }
        public string jobnumber { get; set; }
        public decimal? invoiceamount { get; set; }
        public string payingparty { get; set; }
        public int? payingpartyid { get; set; }
        public string CurrencyCode { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string gstnumber { get; set; }
        public string statename { get; set; }
        public decimal? Taxable value { get; set; }
        public decimal? taxpercent { get; set; }
        public decimal? IGST { get; set; }
        public decimal? Central Tax { get; set; }
        public decimal? State Tax { get; set; }
        public string invoicetypegst { get; set; }
    }
}
