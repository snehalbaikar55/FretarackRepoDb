namespace RepoDbApi.Contracts;

public sealed class BulkCargoHandledByUpdateItem
{
    public int CargoId { get; set; }

    public string? JobNo { get; set; }

    public int StagingRowsUpdated { get; set; }

    public int ShipmentRowsUpdated { get; set; }
}
