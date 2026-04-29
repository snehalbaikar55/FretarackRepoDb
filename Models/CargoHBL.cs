namespace GeneratedModels
{
    public class CargoHBL
    {
        public int HBLID { get; set; }
        public int CargoID { get; set; }
        public bool isDeleted { get; set; }
        public string ForeignPortUnloading { get; set; }
        public string TotalCP { get; set; }
        public string Movement { get; set; }
        public decimal? OriginIHC { get; set; }
        public decimal? OriginTHC { get; set; }
        public decimal? DestinationIHC { get; set; }
        public decimal? DestinationTHC { get; set; }
        public decimal? OceanFreight { get; set; }
        public string BLby { get; set; }
        public int? DocumentTypeID { get; set; }
        public string DocumentType { get; set; }
        public string MBLNumber { get; set; }
        public string HBLNumber { get; set; }
        public string DocumentNumber { get; set; }
        public int? ShipperAddressID { get; set; }
        public string ShipperName { get; set; }
        public string ShipperAddress { get; set; }
        public int? ConsigneeAddressID { get; set; }
        public string ConsigneeName { get; set; }
        public string ConsigneeAddress { get; set; }
        public int? NotifyAddressID { get; set; }
        public string NotifyName { get; set; }
        public string NotifyAddress { get; set; }
        public string ExportReferences { get; set; }
        public int? ForwardingAgentAddressID { get; set; }
        public string ForwardingAgent { get; set; }
        public string ForwardingAgentAddress { get; set; }
        public string PointOfOriginFTZ { get; set; }
        public string DeliveryInstructions { get; set; }
        public string PreCarriageBy { get; set; }
        public string PlaceOfReceipt { get; set; }
        public string VesselVoyage { get; set; }
        public string PortOfLoading { get; set; }
        public string PortOfDischarge { get; set; }
        public string PlaceOfDelivery { get; set; }
        public string LoadingTerminal { get; set; }
        public string TypeOfMove { get; set; }
        public bool? isContainerized { get; set; }
        public string MarksandNumbers { get; set; }
        public string NoOfPackages { get; set; }
        public string DescriptionOfPackagesGoods { get; set; }
        public string GrossWeight { get; set; }
        public string Measurement { get; set; }
        public string DeclaredValue { get; set; }
        public DateTime? HBLDate { get; set; }
        public string HBLPlace { get; set; }
        public string ByAgentForCarrier { get; set; }
        public string FreightCharges { get; set; }
        public string Description1 { get; set; }
        public string Description2 { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
