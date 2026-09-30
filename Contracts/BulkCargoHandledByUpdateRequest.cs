namespace RepoDbApi.Contracts;

public sealed class BulkCargoHandledByUpdateRequest
{
    public List<string> JobNos { get; set; } = new();
}
