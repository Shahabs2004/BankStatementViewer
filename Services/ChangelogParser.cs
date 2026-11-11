namespace BankStatementViewer.Services;

public sealed record ChangelogEntry(string Version, string Date, IReadOnlyList<string> Items, bool IsCurrent);

public static class ChangelogParser
{
    /// <summary>Parses the simple "## version (date)" + "- item" changelog format, newest first.</summary>
    public static List<ChangelogEntry> Parse(string markdown, string? currentVersion)
    {
        var entries = new List<ChangelogEntry>();
        string? version = null, date = null;
        var items = new List<string>();

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (version is not null)
                    entries.Add(new ChangelogEntry(version, date ?? "", items, IsCurrent(version, currentVersion)));

                var header = line[3..].Trim();
                (version, date) = SplitHeader(header);
                items = new List<string>();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) && version is not null)
            {
                items.Add(line[2..].Trim());
            }
        }

        if (version is not null)
            entries.Add(new ChangelogEntry(version, date ?? "", items, IsCurrent(version, currentVersion)));

        return entries;
    }

    private static bool IsCurrent(string version, string? currentVersion) =>
        currentVersion is not null &&
        string.Equals(version.TrimStart('v', 'V'), currentVersion.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase);

    private static (string Version, string Date) SplitHeader(string header)
    {
        var open = header.IndexOf('(');
        var close = header.IndexOf(')');
        if (open > 0 && close > open)
            return (header[..open].Trim(), header[(open + 1)..close].Trim());
        return (header, "");
    }
}
