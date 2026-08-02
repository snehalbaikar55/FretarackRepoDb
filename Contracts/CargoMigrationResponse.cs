using System.Collections.Generic;

namespace RepoDbApi.Contracts;

public sealed class CargoMigrationResponse
{
    public string JobNo { get; set; } = string.Empty;

    public int CargoId { get; set; }

    public bool CargoExistsInCargoMint { get; set; }

    public bool ActualMigrationCompleted { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }

    public Dictionary<string, int> StagingCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
