using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Project.Test;

[TestClass]
public sealed class BundleResourceSizeTests
{
    [TestMethod]
    public void PackageTypeNotifiesAndUpdatesDisplayedSizeInBothDirections()
    {
        var item = new BunleResourceSize(100L * 1024 * 1024 * 1024)
        {
            MinimalSize = 60L * 1024 * 1024 * 1024
        };
        var changed = new List<string?>();
        item.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        item.PackageType = GamePackageType.Minimal;
        Assert.AreEqual("60.00 GB", item.BunleSize);
        CollectionAssert.Contains(changed, nameof(item.BunleSize));
        changed.Clear();
        item.PackageType = GamePackageType.Full;
        Assert.AreEqual("100.00 GB", item.BunleSize);
        CollectionAssert.Contains(changed, nameof(item.BunleSize));
        Assert.AreEqual(100L * 1024 * 1024 * 1024, item.Size);
    }

    [TestMethod]
    public void LoadedMinimalSizeNotifiesExistingBindings()
    {
        var item = new BunleResourceSize(100L * 1024 * 1024 * 1024)
        {
            PackageType = GamePackageType.Minimal
        };
        var changed = new List<string?>();
        item.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        item.MinimalSize = 40L * 1024 * 1024 * 1024;
        Assert.AreEqual("40.00 GB", item.BunleSize);
        CollectionAssert.Contains(changed, nameof(item.BunleSize));
    }
}
