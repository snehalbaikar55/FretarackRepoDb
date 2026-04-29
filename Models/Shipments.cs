namespace GeneratedModels
{
    public class Shipments
    {
        public string ShipmentNo { get; set; }
        public string ShipmentStatus { get; set; }
        public string ShipmentType { get; set; }
        public long ShipmentId { get; set; }
        public int? OrgId { get; set; }
        public bool? IsConsolidated { get; set; }
        public DateTime? ShipmentDate { get; set; }
        public int? PrimaryServiceId { get; set; }
        public string CustomerRefNo { get; set; }
        public string ExternalRemarks { get; set; }
        public string InternalRemarks { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool? IsDeleted { get; set; }
        public string NominationType { get; set; }
        public int? OverseasAgentId { get; set; }
        public int? HandledById { get; set; }
        public string JobNo { get; set; }
        public long? RootShipmentId { get; set; }
        public long? ConsolGroupId { get; set; }
        public int? CustomerId { get; set; }
        public int? BranchId { get; set; }
        public int? SalesPersonId { get; set; }
    }
}
