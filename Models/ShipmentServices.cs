namespace GeneratedModels
{
    public class ShipmentServices
    {
        public string TransportMode { get; set; }
        public string TransportDirection { get; set; }
        public long ShipmentServiceId { get; set; }
        public int? OrgId { get; set; }
        public long? ShipmentId { get; set; }
        public bool? IsPrimary { get; set; }
        public int? LegTypeId { get; set; }
        public int? ServiceSequence { get; set; }
        public int? ServiceTypeId { get; set; }
        public string ConsolidationType { get; set; }
        public int? PickupLocId { get; set; }
        public int? PolId { get; set; }
        public int? PodId { get; set; }
        public int? DeliveryLocId { get; set; }
        public int? ServiceLocationId { get; set; }
        public int? VendorId { get; set; }
        public int? CarrierId { get; set; }
        public DateTime? ETD { get; set; }
        public DateTime? ETA { get; set; }
        public string ServiceNotes { get; set; }
        public string ServiceLegStatus { get; set; }
        public bool? IsHouseRequired { get; set; }
        public bool? IsContainerRequired { get; set; }
        public bool? IsCustomsRequired { get; set; }
        public bool? IsBookingRequired { get; set; }
        public bool? IsConsolidation { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
