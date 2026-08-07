namespace RepoDbApi.Contracts;

public sealed class CargoDocumentMigrationResult
{
    public int TotalRows { get; set; }

    public int MigratedRows { get; set; }

    public int SkippedRows { get; set; }

    public int FailedRows { get; set; }

    public List<CargoDocumentMigrationRowResult> Rows { get; set; } = [];
}

public sealed class CargoDocumentMigrationRowResult
{
    public int FretrackCargoDocumentId { get; set; }

    public int CargoId { get; set; }

    public long? ShipmentId { get; set; }

    public string? JobNo { get; set; }

    public string Status { get; set; } = string.Empty;

    public int? DocumentId { get; set; }

    public string? FilePath { get; set; }

    public string? Message { get; set; }
}
