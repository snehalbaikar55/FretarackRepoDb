namespace GeneratedModels
{
    public class Tasks
    {
        public int TaskID { get; set; }
        public string Subject { get; set; }
        public string Comments { get; set; }
        public int? TaskTypeID { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Priority { get; set; }
        public string TaskStatus { get; set; }
        public int? ContactID { get; set; }
        public string Location { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool? isDeleted { get; set; }
    }
}
