using Haiyu.Helpers;

namespace Haiyu.Models.Wrapper;

public partial class DataCenterBassItemWrapper : ObservableObject
{
    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }
}

public partial class DataCenterRoilItemWrapper : ObservableObject
{
    [ObservableProperty]
    public partial int StarLevel { get; set; }
    public long RoilId { get; }

    [ObservableProperty]
    public partial string RoleIconUrl { get; set; }

    [ObservableProperty]
    public partial string AttributeName { get; set; }

    [ObservableProperty]
    public partial int AttibuteId { get; set; }

    [ObservableProperty]
    public partial BitmapImage TypeImage { get; set; }

    [ObservableProperty]
    public partial string RoleName { get; set; }
    public GameRoilDataItem User { get; }

    public DataCenterRoilItemWrapper(RoleList roleData, GameRoilDataItem user)
    {
        this.StarLevel = roleData.StarLevel;
        this.RoilId = roleData.RoleId;
        this.AttributeName = roleData.AttributeName;
        this.RoleName = roleData.RoleName;
        this.AttibuteId = roleData.AttributeId;
        TypeImage = new BitmapImage(new(RoleHelper.SwitchType(roleData.AttributeId)));
        RoleIconUrl = roleData.RoleIconUrl;
        User = user;
    }

    public DataCenterRoilItemWrapper(RoleList roleData)
    {
        this.StarLevel = roleData.StarLevel;
        this.RoilId = roleData.RoleId;
        this.AttributeName = roleData.AttributeName;
        this.RoleName = roleData.RoleName;
        this.AttibuteId = roleData.AttributeId;
        TypeImage = new BitmapImage(new(RoleHelper.SwitchType(roleData.AttributeId)));
        RoleIconUrl = roleData.RoleIconUrl;
    }

    [RelayCommand]
    void ClickShow()
    {
        if (this.User == null)
            return;
        WeakReferenceMessenger.Default.Send<ShowRoleData>(new ShowRoleData(RoilId, this.User));
    }
}
