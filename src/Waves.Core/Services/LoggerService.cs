using Waves.Core.Models.Options;

namespace Waves.Core.Services;

public delegate void LogMessageHandler(ILogger logger, LogMessage logMessage);

public class LoggerService
{
    public ILogger ILogger { get; private set; }
    public CoreLogOption Option { get; internal set; }

    public LogMessageHandler? logmessageHandler;

    public event LogMessageHandler LogMessageOutput
    {
        add => logmessageHandler += value;
        remove => logmessageHandler -= value;
    }

    public void InitLogger(
        string file,
        RollingInterval time,
        int fileMax = 5120,
        int fileSizeLimitBytes = 10485760,
        CoreLogOption logOption = null
    )
    {
        if (logOption == null)
            this.Option = new CoreLogOption()
            {
                EnableError = true,
                EnableWran = true,
                EnableInfo = true,
            };
        else
            this.Option = logOption;
        ILogger Applogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Async(v =>
            {
                v.File(
                    $"{file}",
                    rollingInterval: time,
                    retainedFileCountLimit: fileMax,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    retainedFileTimeLimit:TimeSpan.FromDays(15)
                );
            })
            .CreateLogger();
        this.ILogger = Applogger;
    }

    public void WriteInfo(string message)
    {
        if (!this.Option.EnableInfo)
            return;
        var dateTime = DateTime.Now;
        ILogger?.Information($"{dateTime.ToString("G")}：{message}");
        logmessageHandler?.Invoke(ILogger, new() { DateTime = dateTime, Message = message });
    }

    public void WriteError(string message)
    {
        if (!this.Option.EnableError)
            return;
        var dateTime = DateTime.Now;
        ILogger?.Error($"{dateTime.ToString("G")}：{message}");
        logmessageHandler?.Invoke(ILogger, new() { DateTime = dateTime, Message = message });
    }

    public void WriteWarning(string message)
    {
        if (!this.Option.EnableWran)
            return;
        var dateTime = DateTime.Now;
        ILogger?.Warning($"{dateTime.ToString("G")}：{message}");
        logmessageHandler?.Invoke(ILogger, new() { DateTime = dateTime, Message = message });
    }
}