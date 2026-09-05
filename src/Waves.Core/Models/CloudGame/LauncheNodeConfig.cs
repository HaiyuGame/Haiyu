namespace Waves.Core.Models.CloudGame;

public class LauncheNodeConfig
{
    public IEnumerable<CloudGameNode> Nodes { get; set; }

    public CloudGameNode? SelectNode { get; set; }
}
