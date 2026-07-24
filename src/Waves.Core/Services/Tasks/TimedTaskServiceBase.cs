using Waves.Core.Contracts.Tasks;

namespace Waves.Core.Services.Tasks;

/// <summary>
/// 每天在指定的本地时间执行一次的任务基类。
/// </summary>
public abstract class TimedTaskServiceBase : TimerTaskServiceBase
{
    private int _lastTriggeredDayNumber = -1;

    protected TimedTaskServiceBase(
        SystemEventPublisher publisher,
        LoggerService logger
    )
        : base(publisher, logger)
    {
        CheckDelay = 30;
    }


    /// <summary>
    /// 每天执行任务的本地时间。
    /// </summary>
    public TimeOnly TargetTime { get; set; }

    /// <summary>
    /// 到达目标时间后的允许触发时长。
    /// </summary>
    public TimeSpan TimeTolerance { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 获取当前本地时间。派生类或测试可以重写此属性。
    /// </summary>
    protected virtual DateTime LocalNow => DateTime.Now;

    public override Task InitializationAsync()
    {
        if (TimeTolerance <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TimeTolerance),
                TimeTolerance,
                "时间容差必须大于零。"
            );
        }

        if (TimeSpan.FromSeconds(CheckDelay) > TimeTolerance)
        {
            throw new InvalidOperationException("任务检查周期不能大于时间容差，否则可能错过目标时间。");
        }

        return base.InitializationAsync();
    }

    public override bool CheckRun()
    {
        var now = LocalNow;
        var today = DateOnly.FromDateTime(now);
        var target = today.ToDateTime(TargetTime);

        if (now < target || now - target >= TimeTolerance)
            return false;

        var dayNumber = today.DayNumber;
        return Interlocked.Exchange(ref _lastTriggeredDayNumber, dayNumber) != dayNumber;
    }
}
