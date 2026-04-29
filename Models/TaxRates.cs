namespace GeneratedModels
{
    public class TaxRates
    {
        public int TaxRateID { get; set; }
        public int TaxID { get; set; }
        public string TaxName { get; set; }
        public string TaxCode { get; set; }
        public decimal? EffectiveTaxPercentage { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }
}
