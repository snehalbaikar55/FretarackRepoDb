namespace GeneratedModels
{
    public class AirShipmentRouting
    {
        public int AirRoutingID { get; set; }
        public int ShipmentID { get; set; }
        public int RoutingOrder { get; set; }
        public bool isDeleted { get; set; }
        public bool? isPOT1 { get; set; }
        public bool? isPOT2 { get; set; }
        public string AirCarrierName { get; set; }
        public DateTime? BookingDate { get; set; }
        public string BookingNumber { get; set; }
        public string igmno { get; set; }
        public int? AirCarrierID { get; set; }
        public string FlightNumber { get; set; }
        public int? POLID { get; set; }
        public string POL { get; set; }
        public int? PODID { get; set; }
        public string POD { get; set; }
        public DateTime? ETD { get; set; }
        public DateTime? ATD { get; set; }
        public DateTime? ETA { get; set; }
        public DateTime? ATA { get; set; }
        public int? POT1ID { get; set; }
        public string POT1 { get; set; }
        public DateTime? POT1Date { get; set; }
        public int? POT2ID { get; set; }
        public string POT2 { get; set; }
        public DateTime? POT2Date { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
