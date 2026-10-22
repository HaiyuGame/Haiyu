namespace Haiyu.Models.Wrapper;

public sealed partial class RecordActivityFiveStarItemWrapper : ObservableObject
{
    [ObservableProperty]
    public partial BitmapImage Icon { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool? Flage { get; set; }

    [ObservableProperty]
    public partial Visibility ShowFlage { get; set; }
}

public static partial class WrapperCollectionExtension
{
    // 判断歪不歪
    public static IEnumerable<RecordActivityFiveStarItemWrapper> Format(
        this IEnumerable<Tuple<RecordCardItemWrapper, int, bool?>> values,
        IEnumerable<CommunityRoleData>? roles,
        bool showFlag
    )
    {
        List<RecordActivityFiveStarItemWrapper> item = new();
        if (roles == null)
            return item;
        foreach (var value in values)
        {
            var resource = roles.Where(x => x.Id == value.Item1.ResourceId).FirstOrDefault();
            if (resource == null)
            {
                item.Add(
                    new RecordActivityFiveStarItemWrapper()
                    {
                        Count = value.Item2,
                        Name = value.Item1.Name,
                        Icon = null,
                        Flage = showFlag == true ? IsResident(value.Item1.ResourceId) : false,
                    }
                );
            }
            else
            {
                item.Add(
                    new RecordActivityFiveStarItemWrapper()
                    {
                        Count = value.Item2,
                        Name = value.Item1.Name,
                        Icon = new(new($"https://mc.appfeng.com/ui/avatar/{resource.Icon}.png")),
                        Flage = showFlag == true ? IsResident(value.Item1.ResourceId) : false,
                    }
                );
            }
        }
        return item;
    }

    public static IEnumerable<RecordActivityFiveStarItemWrapper> Format(
        this IEnumerable<Tuple<RecordCardItemWrapper, int, bool?>> values,
        IEnumerable<CommunityWeaponData>? roles,
        bool showFlag
    )
    {
        List<RecordActivityFiveStarItemWrapper> item = new();
        if (roles == null)
            return item;
        foreach (var value in values)
        {
            var resource = roles.Where(x => x.Id == value.Item1.ResourceId).First();
            if (resource == null)
            {
                continue;
            }
            item.Add(
                new RecordActivityFiveStarItemWrapper()
                {
                    Count = value.Item2,
                    Name = value.Item1.Name,
                    Icon = new(new($"https://mc.appfeng.com/ui/weapon/{resource.Icon}.png")),
                    Flage = showFlag == true ? value.Item3 : false,
                }
            );
        }
        return item;
    }

    /// <summary>
    /// 离线判断是否为常驻
    /// </summary>
    /// <returns></returns>
    public static bool IsResident(int id)
    {
        return RecordHelper.IsResident(id);
    }

    public static IEnumerable<RecordActivityFiveStarItemWrapper> Format(
        this IEnumerable<Tuple<RecordCardItemWrapper, int>> values,
        IEnumerable<CommunityWeaponData>? roles,
        bool showFlag
    )
    {
        List<RecordActivityFiveStarItemWrapper> item = new();
        foreach (var value in values)
        {
            var resource = roles.Where(x => x.Id == value.Item1.ResourceId).First();
            if (resource == null)
            {
                continue;
            }
            item.Add(
                new RecordActivityFiveStarItemWrapper()
                {
                    Count = value.Item2,
                    Name = value.Item1.Name,
                    Icon = new(new($"https://mc.appfeng.com/ui/weapon/{resource.Icon}.png")),
                    Flage = null,
                    ShowFlage = showFlag == true ? Visibility.Visible : Visibility.Collapsed,
                }
            );
        }
        return item;
    }

    public static IEnumerable<RecordActivityFiveStarItemWrapper> Format(
        this IEnumerable<Tuple<RecordCardItemWrapper, int>> values,
        IEnumerable<CommunityRoleData>? roles,
        bool showFlag
    )
    {
        List<RecordActivityFiveStarItemWrapper> item = new();
        foreach (var value in values)
        {
            var resource = roles.Where(x => x.Id == value.Item1.ResourceId).First();
            if (resource == null)
            {
                continue;
            }
            item.Add(
                new RecordActivityFiveStarItemWrapper()
                {
                    Count = value.Item2,
                    Name = value.Item1.Name,
                    Icon = new(new($"https://mc.appfeng.com/ui/avatar/{resource.Icon}.png")),
                    Flage = null,
                    ShowFlage = showFlag == true ? Visibility.Visible : Visibility.Collapsed,
                }
            );
        }
        return item;
    }
}
