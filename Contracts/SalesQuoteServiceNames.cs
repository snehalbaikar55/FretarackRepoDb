namespace RepoDbApi.Contracts;

public static class SalesQuoteServiceNames
{
    private static readonly string[] MasterNames =
    [
        "Air", "FCL", "LCL", "Trucking", "Rail", "Warehouse",
        "Custom Clearance", "Courier", "Breakbulk", "Documentation"
    ];

    // Match names, never infer CargoMint numeric IDs from source IDs.
    public static string? Normalize(string? value, string? transportMode = null)
    {
        var name = Clean(value);
        var mode = Clean(transportMode);
        if (mode.Length > 0 && name.EndsWith(" " + mode, StringComparison.OrdinalIgnoreCase))
            name = name[..^(mode.Length + 1)].Trim();
        foreach (var suffix in new[] { " EXPORT", " IMPORT" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length].Trim();
                break;
            }
        return MasterName(name);
    }

    public static string? MasterName(string? value) => MasterNames.FirstOrDefault(master =>
        master.Equals(Clean(value), StringComparison.OrdinalIgnoreCase));

    public static string? Label(string? value, string? transportMode = null) =>
        Normalize(value, transportMode) ?? (string.IsNullOrWhiteSpace(value) ? null : value.Trim());

    private static string Clean(string? value) => string.Join(" ",
        (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
