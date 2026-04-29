namespace GeneratedModels
{
    public class PaymentTerms
    {
        public int PaymentTermID { get; set; }
        public bool isActive { get; set; }
        public string PaymentTerm { get; set; }
        public byte? DueDays { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public byte? DiscountDays { get; set; }
    }
}
