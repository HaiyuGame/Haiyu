using Waves.Api.Models.Communitys.DataCenter;

namespace Haiyu.Model;

/// <summary>角色下拉框专用的 AOT 可投影项，不将网络 DTO 直接传过 WinRT ABI。</summary>
public sealed partial class SortingRoleOption(RoleList role)
{
    public long RoleId { get; } = role.RoleId;
    public string RoleName { get; } = role.RoleName;
    public int Level { get; } = role.Level;
    public string AttributeName { get; } = role.AttributeName;
    public string IconUrl { get; } = role.RoleIconUrl ?? "";
    public string PortraitUrl { get; } = role.RolePicUrl ?? "";
}
