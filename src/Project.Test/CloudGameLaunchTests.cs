using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models.CloudGame;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.GameContext;
using Waves.Core.Models;
using Waves.Core.Models.CloudGame;
using Waves.Core.Services;
using Waves.Core.Services.CloudGameServices;

namespace Project.Test;

[TestClass]
public sealed class CloudGameLaunchTests
{
    [TestMethod]
    [DoNotParallelize]
    public async Task AutoSelectionDefaultsOnAndRemembersAnExplicitOptOut()
    {
        var directory = Directory.CreateTempSubdirectory("haiyu-cloud-test-");
        var previousGamePath = GameContextFactory.GameBassPath;
        try
        {
            GameContextFactory.GameBassPath = directory.FullName;
            var config = new GameLocalConfig(
                Path.Combine(directory.FullName, "WavesCloudConfig", "Settings.bat")
            );
            Assert.IsTrue(await config.SaveConfigAsync(CloudGameLocalSettingName.Fps, "60"));

            await using var services = new ServiceCollection()
                .AddGameContext()
                .AddSingleton(new CloudConfigManager(directory.FullName))
                .AddSingleton<IWavesCloudGameService, WavesCloudGameService>()
                .BuildServiceProvider();
            var context = services.GetRequiredKeyedService<IKuroCloudGameContext>(
                nameof(KuroCloudGameContext)
            );
            try
            {
                await context.InitAsync();
                Assert.IsTrue(context.AutoSelectLowestLatencyNode);

                Assert.IsTrue(await context.SetAutoSelectLowestLatencyNodeAsync(false));
                await context.InitAsync();

                Assert.IsFalse(context.AutoSelectLowestLatencyNode);
                Assert.AreEqual(
                    "60",
                    await context.GameLocalConfig.GetConfigAsync(CloudGameLocalSettingName.Fps)
                );
            }
            finally
            {
                await context.CloudGameProcessTracker.DisposeAsync();
            }
        }
        finally
        {
            GameContextFactory.GameBassPath = previousGamePath;
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void StartTargetsTheFastestMeasuredNodeThatTheServerOffered()
    {
        CloudGameNode[] regions =
        [
            new() { RegionName = "Empty", NodeList = [] },
            new() { RegionName = "Unmeasured", NodeList = [new() { NodeId = "unknown" }] },
            new()
            {
                RegionName = "A",
                NodeList =
                [
                    new() { NodeId = "a-1", Delay = 1 },
                    new() { NodeId = "a-2", Delay = 120 },
                ],
            },
            new() { RegionName = "B", NodeList = [new() { NodeId = "b-1", Delay = 10 }] },
        ];
        var measurements = new Dictionary<string, int>
        {
            ["a-1"] = 90,
            ["a-2"] = 18,
            ["b-1"] = 25,
            ["not-offered"] = 2,
        };

        var selection = LauncheNodeConfig.SelectFastest(regions, measurements);

        Assert.IsNotNull(selection?.SelectNode);
        Assert.AreEqual("A", selection.SelectNode.RegionName);
        Assert.AreEqual("a-2", selection.SelectNode.NodeList.Single().NodeId);
        Assert.AreEqual(18, selection.SelectNode.Delay);
        CollectionAssert.AreEqual(
            new[] { "A", "B" },
            selection.Nodes.Select(node => node.RegionName).ToArray()
        );
    }

    [TestMethod]
    public void StartHasNoTargetWhenAllOfferedNodesFailedToMeasure()
    {
        CloudGameNode[] regions =
        [
            new() { NodeList = [] },
            new() { NodeList = [new() { NodeId = "unknown" }, new() { NodeId = "timed-out" }] },
        ];

        var selection = LauncheNodeConfig.SelectFastest(
            regions,
            new Dictionary<string, int> { ["timed-out"] = -1, ["not-offered"] = 5 }
        );

        Assert.IsNull(selection);
    }
}
