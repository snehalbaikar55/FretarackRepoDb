namespace RepoDbApi.Contracts;

public sealed class CargoMigrationRequest
{
    public string JobNo { get; set; } = string.Empty;

    public int OrgId { get; set; } = 18;
}
