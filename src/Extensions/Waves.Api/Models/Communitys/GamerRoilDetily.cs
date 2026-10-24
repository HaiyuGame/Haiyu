using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Waves.Api.Models.Communitys;

public partial class ChainList : ObservableObject
{
    [JsonPropertyName("description")]
    [ObservableProperty]
    public partial string Description { get; set; }

    [JsonPropertyName("iconUrl")]
    [ObservableProperty]
    public partial string IconUrl { get; set; }

    [JsonPropertyName("name")]
    [ObservableProperty]
    public partial string Name { get; set; }

    [JsonPropertyName("order")]
    [ObservableProperty]
    public partial int Order { get; set; }

    [JsonPropertyName("unlocked")]
    [ObservableProperty]
    public partial bool Unlocked { get; set; }
}

public class EquipPhantomList
{
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("fetterDetail")]
    public FetterDetail FetterDetail { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("mainProps")]
    public List<MainProp> MainProps { get; set; }

    [JsonPropertyName("phantomProp")]
    public PhantomProp PhantomProp { get; set; }

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("subProps")]
    public List<SubProp> SubProps { get; set; }
}

public class FetterDetail
{
    [JsonPropertyName("firstDescription")]
    public string FirstDescription { get; set; }

    [JsonPropertyName("groupId")]
    public int GroupId { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("secondDescription")]
    public string SecondDescription { get; set; }
}

public class MainProp
{
    [JsonPropertyName("attributeName")]
    public string AttributeName { get; set; }

    [JsonPropertyName("attributeValue")]
    public string AttributeValue { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }
}

public class PhantomData
{
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("equipPhantomList")]
    public List<EquipPhantomList> EquipPhantomList { get; set; }
}

public class PhantomProp
{
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("phantomId")]
    public int PhantomId { get; set; }

    [JsonPropertyName("phantomPropId")]
    public int PhantomPropId { get; set; }

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("skillDescription")]
    public string SkillDescription { get; set; }
}

public class Role
{
    [JsonPropertyName("acronym")]
    public string Acronym { get; set; }

    [JsonPropertyName("attributeId")]
    public int AttributeId { get; set; }

    [JsonPropertyName("attributeName")]
    public string AttributeName { get; set; }

    [JsonPropertyName("breach")]
    public int Breach { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("roleIconUrl")]
    public string RoleIconUrl { get; set; }

    [JsonPropertyName("roleId")]
    public int RoleId { get; set; }

    [JsonPropertyName("roleName")]
    public string RoleName { get; set; }

    [JsonPropertyName("rolePicUrl")]
    public string RolePicUrl { get; set; }

    [JsonPropertyName("starLevel")]
    public int StarLevel { get; set; }

    [JsonPropertyName("weaponTypeId")]
    public int WeaponTypeId { get; set; }

    [JsonPropertyName("weaponTypeName")]
    public string WeaponTypeName { get; set; }
}

public class GamerRoilDetily
{
    /// <summary>接口返回的角色实际面板，与声骸附加属性列表区分，未返回时保留为空。</summary>
    [JsonPropertyName("roleAttributeList")]
    public List<MainProp>? RoleAttributeList { get; set; }

    /// <summary>当前角色皮肤的立绘信息。</summary>
    [JsonPropertyName("roleSkin")]
    public RoleSkinData? RoleSkin { get; set; }
    [JsonPropertyName("chainList")]
    public List<ChainList> ChainList { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("phantomData")]
    public PhantomData PhantomData { get; set; }

    [JsonPropertyName("role")]
    public Role Role { get; set; }

    [JsonPropertyName("skillList")]
    public List<SkillList> SkillList { get; set; }

    [JsonPropertyName("weaponData")]
    public WeaponData WeaponData { get; set; }
}

public sealed class RoleSkinData
{
    [JsonPropertyName("picUrl")]
    public string? PicUrl { get; set; }
    [JsonPropertyName("skinIcon")]
    public string? SkinIcon { get; set; }
}

public class Skill
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("iconUrl")]
    public string IconUrl { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}

public class SkillList
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("skill")]
    public Skill Skill { get; set; }
}

public class SubProp
{
    [JsonPropertyName("attributeName")]
    public string AttributeName { get; set; }

    [JsonPropertyName("attributeValue")]
    public string AttributeValue { get; set; }
}

public class Weapon
{
    [JsonPropertyName("effectDescription")]
    public string EffectDescription { get; set; }

    [JsonPropertyName("weaponEffectName")]
    public string WeaponEffectName { get; set; }

    [JsonPropertyName("weaponIcon")]
    public string WeaponIcon { get; set; }

    [JsonPropertyName("weaponId")]
    public int WeaponId { get; set; }

    [JsonPropertyName("weaponName")]
    public string WeaponName { get; set; }

    [JsonPropertyName("weaponStarLevel")]
    public int WeaponStarLevel { get; set; }

    [JsonPropertyName("weaponType")]
    public int WeaponType { get; set; }
}

public class WeaponData
{
    [JsonPropertyName("breach")]
    public int Breach { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("resonLevel")]
    public int ResonLevel { get; set; }

    [JsonPropertyName("weapon")]
    public Weapon Weapon { get; set; }
}
