namespace RepoDbApi.Contracts;

public sealed class BulkCargoHandledByUpdateResponse
{
    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }

    public List<BulkCargoHandledByUpdateItem> Items { get; set; } = new();

    public int TotalCargoCount => Items.Count;

    public int TotalStagingRowsUpdated { get; set; }

    public int TotalShipmentRowsUpdated { get; set; }
}
