namespace RepoDbApi.Contracts;

public sealed class ConnectionTestResponse
{
    public string ConnectionName { get; set; } = string.Empty;

    public bool IsConnected { get; set; }

    public string? Message { get; set; }

    public string? Database { get; set; }

    public string? DataSource { get; set; }
}
