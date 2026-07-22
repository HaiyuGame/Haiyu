namespace Waves.Core.Contracts.Tasks;

public interface ITimerTaskService:ITaskService
{
    /// <summary>
    /// 检查周期
    /// </summary>
    public long CheckDelay { get; set; }

    /// <summary>
    /// 检查是否可以执行
    /// </summary>
    /// <returns></returns>
    public bool CheckRun();
}
