using Waves.Core.Models;

namespace Waves.Core.Contracts;

/// <summary>
/// 通用本地账号接口，目前只实现库街区账号
/// </summary>
public interface IKuroAccountService
{

    public LocalAccount? Current { get; }

    /// <summary>
    /// 获得库街区所有账号
    /// </summary>
    /// <returns></returns>
    public Task<List<LocalAccount>> GetUsersAsync();

    /// <summary>
    /// 获得某个账号
    /// </summary>
    /// <returns></returns>
    public Task<LocalAccount?> GetUserAsync(string userId);

    /// <summary>
    /// 保存账号信息
    /// </summary>
    /// <returns></returns>
    public Task<bool> SaveUserAsync(LocalAccount localAccount);

    /// <summary>
    /// 删除账号信息
    /// </summary>
    /// <returns></returns>
    public Task<bool> DeleteUserAsync(string userId);


    /// <summary>
    /// 设置当前账号
    /// </summary>
    /// <param name="localAccount"></param>
    public void SetCurrentUser(string userId, bool isWrite = true);
    public void SetCurrentUser(LocalAccount user, bool isWrite = true);
    Task SetAutoUser();
}
