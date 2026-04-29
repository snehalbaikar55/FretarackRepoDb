namespace GeneratedModels
{
    public class OceanSchedules
    {
        public int POLID { get; set; }
        public int PODID { get; set; }
        public int OceanScheduleID { get; set; }
        public int OceanLineID { get; set; }
        public int VesselID { get; set; }
        public bool isDeleted { get; set; }
        public string Voyage { get; set; }
        public DateTime? DateDocumentCutoff { get; set; }
        public DateTime? DatePortCutoff { get; set; }
        public DateTime? ETD { get; set; }
        public DateTime? ETA { get; set; }
        public string Frequency { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
    }
}
