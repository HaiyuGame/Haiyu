namespace Waves.Core.Models.CloudGame;

public class LauncheNodeConfig
{
    public IEnumerable<CloudGameNode> Nodes { get; set; }

    public CloudGameNode? SelectNode { get; set; }

    public static LauncheNodeConfig? SelectFastest(
        IEnumerable<CloudGameNode> regions,
        IReadOnlyDictionary<string, int> measurements
    )
    {
        var nodes = new List<CloudGameNode>();
        foreach (var region in regions)
        {
            if (region?.NodeList is not { Count: > 0 } candidates)
                continue;

            var fastest = candidates
                .Where(node => !string.IsNullOrWhiteSpace(node?.NodeId))
                .Select(node => new NodeList
                {
                    NodeId = node.NodeId,
                    Delay = measurements.GetValueOrDefault(node.NodeId, -1),
                })
                .Where(node => node.Delay >= 0)
                .MinBy(node => node.Delay);

            if (fastest is not null)
                nodes.Add(new CloudGameNode { RegionName = region.RegionName, NodeList = [fastest] });
        }

        var selectedNode = nodes.MinBy(node => node.Delay);
        if (selectedNode is null)
            return null;

        return new LauncheNodeConfig { Nodes = nodes, SelectNode = selectedNode };
    }
}
