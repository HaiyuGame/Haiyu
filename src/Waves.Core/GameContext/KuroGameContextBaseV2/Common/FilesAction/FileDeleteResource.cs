namespace Waves.Core.GameContext.Common.FilesAction;

/// <summary>有界并发删除清单文件；不递归删除目录。</summary>
internal sealed class FileDeleteResource : IProgressSetup, IAsyncDisposable
{
    private string[] _files = [];
    private IGameEventPublisher<GameContextOutputArgs> _publisher = null!;
    private CancellationTokenSource? _lifetime;
    private readonly object _progressGate = new();
    private int _completed;
    private long _generation;
    public string ProgressName { get; set; } = "删除旧文件";
    public double ProgressValue { get; private set; }
    public bool CanPause => false;
    public bool CanStop => false;

    public void SetParam(Dictionary<string, object> param,
        IGameEventPublisher<GameContextOutputArgs> gameEventPublisher)
    {
        if (!param.TryGetValue("files", out var files) || files is not IEnumerable<string> paths)
            throw new ArgumentException("缺少待删除文件清单。", nameof(param));
        _files = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var token = param.TryGetValue("downloadState", out var state) && state is DownloadState download
            ? download.CancelToken.Token : CancellationToken.None;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        _publisher = gameEventPublisher;
        _generation = GameContextOutputArgs.CurrentGeneration.Value;
    }

    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (_lifetime is null) throw new InvalidOperationException("请先设置删除参数。");
        var token = _lifetime.Token;
        _completed = 0;
        Report("");
        await Task.Run(async () =>
        {
            await Parallel.ForEachAsync(_files, new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = token
            }, (path, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                File.Delete(path);
                lock (_progressGate)
                {
                    _completed++;
                    Report(path);
                }
                return ValueTask.CompletedTask;
            });
        }, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return true;
    }

    private void Report(string path)
    {
        ProgressValue = _files.Length == 0 ? 1 : (double)_completed / _files.Length;
        var message = $"{ProgressName} {_completed}/{_files.Length} {Path.GetFileName(path)}";
        _publisher.Publish(new GameContextOutputArgs
        {
            Generation = _generation,
            Type = GameContextActionType.BottomText,
            IsAction = true,
            CurrentFile = _completed,
            FileTotal = _files.Length,
            DeleteString = message,
            TipMessage = message
        });
    }

    public ValueTask DisposeAsync()
    {
        _lifetime?.Cancel();
        return ValueTask.CompletedTask;
    }
}

