using System.Threading.Channels;
namespace RepoDbApi.Services;

// Request-local progress; no job data or progress is shared between requests.
public static class MigrationProgress
{
    public static readonly AsyncLocal<ChannelWriter<object>?> Current = new();
    public static void Report(string section, string status, int? count = null) =>
        Current.Value?.TryWrite(new { kind = "progress", section, status, count });
}
