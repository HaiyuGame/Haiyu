namespace Waves.Core.Services.GameResourceProvider;

internal static class BundleIndexFilter
{
    public static PatchIndexGameResource Apply(PatchIndexGameResource index, GamePackageType? packageType)
    {
        var rule = index.TagGroups?.FirstOrDefault(x =>
            string.Equals(x.Key, "min", StringComparison.OrdinalIgnoreCase)).Value;
        if (packageType != GamePackageType.Minimal || rule is null)
            return index;

        bool Matches(string path) => Match(path, rule);
        var carriers = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool Keep(string dest, IEnumerable<IndexResource>? entries)
        {
            var keep = entries is null || !entries.Any() || entries.Any(x => Matches(x.Dest));
            carriers[dest] = carriers.GetValueOrDefault(dest) || keep;
            return keep;
        }
        var zips = index.ZipFileInfos?.Where(x => Keep(x.Dest, x.Entries)).ToList();
        var patches = index.PatchInfos?.Where(x => Keep(x.Dest, x.Entries)).ToList();
        var groups = index.GroupInfos?.Where(x => Keep(x.Dest, x.DstFiles)).ToList();
        List<IndexResource>? Filter(List<IndexResource>? files) => files?.Where(x =>
            carriers.TryGetValue(x.Dest, out var keep) ? keep : Matches(x.Dest)).ToList();
        return new PatchIndexGameResource
        {
            TagGroups = index.TagGroups,
            ApplyTypes = index.ApplyTypes,
            Resource = Filter(index.Resource)!,
            GroupResource = Filter(index.GroupResource),
            ZipFileInfos = zips!,
            PatchInfos = patches!,
            GroupInfos = groups!,
            DeleteFiles = index.DeleteFiles?.Where(Matches).ToList()!
        };
    }

    private static bool Match(string path, ResourceTagRule rule)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        path = Normalize(path);
        bool StartsWith(string prefix) => path.StartsWith(Normalize(prefix), StringComparison.OrdinalIgnoreCase);
        if (rule.ExcludePrefixes is { Count: > 0 })
            return !rule.ExcludePrefixes.Any(StartsWith);
        return rule.Exact?.Any(x => string.Equals(path, Normalize(x), StringComparison.OrdinalIgnoreCase)) == true
            || rule.Prefixes?.Any(StartsWith) == true;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
