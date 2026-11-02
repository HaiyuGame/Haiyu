using System.Collections.Generic;
using System.Threading.Tasks;
using Waves.Api.Models.Record;

namespace Haiyu.Plugin.Contracts;

public interface IWavesPlayerCardCacheServices
{
    /// <summary>
    /// 保存抽卡
    /// </summary>
    /// <param name="card"></param>
    /// <returns></returns>
    Task<Tuple<WavesAnalysisPlayerCard, string>> SaveAsync(WavesAnalysisPlayerCard card);
    /// <summary>
    /// 加载抽卡
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    Task<WavesAnalysisPlayerCard?> LoadAsync(string sessionId);

    /// <summary>
    /// 加载全部抽卡记录
    /// </summary>
    /// <returns></returns>
    Task<List<WavesAnalysisPlayerCard>> LoadAllAsync();

    /// <summary>
    /// 删除抽卡记录
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    Task DeleteAsync(string sessionId);
    
    /// <summary>
    /// 查看抽卡记录是否存在
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    Task<bool> ExistsAsync(string sessionId);
}
