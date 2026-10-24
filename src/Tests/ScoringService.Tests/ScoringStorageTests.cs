using System.Text.Json;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService.Tests;

[TestClass]
public sealed class ScoringStorageTests
{
    private string root = "";
    private ScoringEntryService store = null!;

    [TestInitialize]
    public void Initialize()
    {
        root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Haiyu.SortingTests-" + Guid.NewGuid().ToString("N")));
        store = new(new() { GroupFolder = Path.Combine(root, "Groups"), ItemFolder = Path.Combine(root, "Items") });
    }

    [TestCleanup]
    public void Cleanup()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(root).StartsWith("Haiyu.SortingTests-", StringComparison.Ordinal) && Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task MissingDirectoriesAreEmptyLibraries()
    {
        Assert.AreEqual(0, (await store.GetGroupTemplateAsync()).Count());
        Assert.AreEqual(0, (await store.GetItemsAsync()).Count);
    }

    [TestMethod]
    public async Task GroupsRoundTripAndRejectAccidentalOverwrite()
    {
        var group = Group();
        await store.SaveGroupAsync(group);
        group.Name = "更新模板";
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.SaveGroupAsync(group));
        Assert.AreEqual("测试模板", (await store.GetGroupTemplateAsync()).Single().Name);
        await store.SaveGroupAsync(group, overwrite: true);
        Assert.AreEqual("更新模板", (await store.GetGroupTemplateAsync()).Single().Name);
        Assert.AreEqual(1, Directory.GetFiles(store.Option.GroupFolder, "*.json").Length);
        Assert.AreEqual(0, Directory.GetFiles(store.Option.GroupFolder, "*.tmp").Length);
    }

    [TestMethod]
    public async Task ExistingCustomFilenameIsUpdatedInPlace()
    {
        Directory.CreateDirectory(store.Option.GroupFolder);
        var path = Path.Combine(store.Option.GroupFolder, "old-template.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Group(), ScoringJsonContext.Default.EntryGroup));
        var changed = Group(); changed.Name = "旧文件更新";
        await store.SaveGroupAsync(changed, overwrite: true);
        Assert.AreEqual(path, Directory.GetFiles(store.Option.GroupFolder, "*.json").Single());
        Assert.AreEqual("旧文件更新", (await store.GetGroupTemplateAsync()).Single().Name);
    }

    [TestMethod]
    public async Task LibraryKeySeparatesUsageAndUnit()
    {
        await store.SaveItemAsync(Item());
        var fixedValue = Item(); fixedValue.Type = EntryType.Value;
        await store.SaveItemAsync(fixedValue);
        var main = Item(); main.Usage = EntryUsage.MainStatScoring;
        await store.SaveItemAsync(main);
        Assert.AreEqual(3, (await store.GetItemsAsync()).Count);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.SaveItemAsync(Item()));
    }

    [TestMethod]
    public async Task DeletingLibraryItemDoesNotModifyTemplateCopy()
    {
        await store.SaveItemAsync(Item());
        await store.SaveGroupAsync(Group());
        await store.DeleteItemAsync(Item());
        Assert.AreEqual(0, (await store.GetItemsAsync()).Count);
        Assert.AreEqual(1, (await store.GetGroupTemplateAsync()).Single().Items.Count);
        await store.DeleteGroupAsync("group-test");
        Assert.AreEqual(0, (await store.GetGroupTemplateAsync()).Count());
    }

    [TestMethod]
    public async Task IdentifierIsNeverUsedAsAFilePath()
    {
        var group = Group(); group.Id = "../../outside/模板";
        await store.SaveGroupAsync(group);
        var file = Directory.GetFiles(store.Option.GroupFolder, "*.json").Single();
        Assert.AreEqual(64, Path.GetFileNameWithoutExtension(file).Length);
        Assert.AreEqual(group.Id, (await store.GetGroupTemplateAsync()).Single().Id);
        await store.DeleteGroupAsync(group.Id);
        Assert.AreEqual(0, Directory.GetFiles(store.Option.GroupFolder).Length);
    }

    [TestMethod]
    public async Task InvalidImportDoesNotCreateFiles()
    {
        var group = Group(); group.MainStatWeight = 2;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveGroupAsync(group));
        var item = Item(); item.PenaltyCoefficient = 0.3m;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveItemAsync(item));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public async Task MalformedJsonIsReportedInsteadOfSilentlyIgnored()
    {
        Directory.CreateDirectory(store.Option.GroupFolder);
        await File.WriteAllTextAsync(Path.Combine(store.Option.GroupFolder, "broken.json"), "{");
        await Assert.ThrowsExceptionAsync<JsonException>(() => store.GetGroupTemplateAsync());
    }

    [TestMethod]
    public async Task ConcurrentCreatesWithSameKeyOnlySaveOnce()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try { await store.SaveGroupAsync(Group()); return true; }
            catch (InvalidOperationException) { return false; }
        }));
        Assert.AreEqual(1, results.Count(x => x));
        Assert.AreEqual(1, (await store.GetGroupTemplateAsync()).Count());
    }

    [TestMethod]
    public async Task ChangeNotificationOnlyFollowsSuccessfulOperations()
    {
        var notifications = 0;
        store.Changed += (_, _) => notifications++;
        await store.SaveItemAsync(Item());
        Assert.AreEqual(1, notifications);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.SaveItemAsync(Item()));
        Assert.AreEqual(1, notifications);
        await store.DeleteItemAsync(Item());
        Assert.AreEqual(2, notifications);
    }

    private static EntryItem Item() => new()
    {
        EntryNo = "atk", EntryName = "攻击力加成", Type = EntryType.Percentage,
        Usage = EntryUsage.SubStatScoring, ReferenceValue = 10, Weight = 1
    };

    private static EntryGroup Group() => new()
    {
        Id = "group-test", Name = "测试模板", RoleId = "role-test",
        MainStatWeight = 0, SubStatWeight = 1,
        MainStatMaximumBenefit = 1, SubStatMaximumBenefit = 1, Items = [Item()]
    };
}
