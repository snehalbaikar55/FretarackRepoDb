namespace GeneratedModels
{
    public class CargoStatusMaster
    {
        public int StatusID { get; set; }
        public string StatusServiceType { get; set; }
        public string StatusName { get; set; }
        public bool isWebTracking { get; set; }
        public bool isSystemGenerated { get; set; }
        public int? StatusStageNumber { get; set; }
        public int? StagePercentage { get; set; }
        public bool? isCompulsary { get; set; }
        public int? RemindBefore { get; set; }
        public int? AutoTaskTemplateID { get; set; }
        public int? RelatedCargoDocumentID { get; set; }
        public string TransportDirection { get; set; }
        public string StatusReferenceNumber { get; set; }
    }
}
