using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.Publish.Services;

namespace Haiyu.Publish.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ReleaseBuildService _service;
    private readonly ConcurrentQueue<string> _pendingLogs = new();
    private readonly DispatcherTimer _logFlushTimer;

    [ObservableProperty] private string version = "1.0.0";
    [ObservableProperty] private string configuration = "Release";
    [ObservableProperty] private string repositoryRoot;
    [ObservableProperty] private string outputPath;
    [ObservableProperty] private string log = "";
    [ObservableProperty] private string status = "\u51c6\u5907\u5c31\u7eea";
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool isBuilding;
    [ObservableProperty] private bool buildExe = true;
    [ObservableProperty] private bool buildZip = true;
    [ObservableProperty] private bool buildMsix = true;

    public bool CanBuild => !IsBuilding;

    public MainWindowViewModel()
    {
        RepositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        OutputPath = Path.Combine(RepositoryRoot, "artifacts", "release");
        _service = new ReleaseBuildService(RepositoryRoot);
        Version = _service.ReadCurrentVersion();
        _service.LogReceived += line => _pendingLogs.Enqueue(line);
        _service.ProgressChanged += (value, text) => App.Current.Dispatcher.BeginInvoke(() => { Progress = value; Status = text; });
        _logFlushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _logFlushTimer.Tick += (_, _) => FlushPendingLogs();
        _logFlushTimer.Start();
    }

    partial void OnIsBuildingChanged(bool value) => OnPropertyChanged(nameof(CanBuild));

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildAsync()
    {
        if (!Regex.IsMatch(Version.Trim(), @"^\d+\.\d+\.\d+(\.\d+)?$"))
        {
            Status = "\u7248\u672c\u53f7\u683c\u5f0f\u5e94\u4e3a 1.2.3 \u6216 1.2.3.4";
            return;
        }
        if (!BuildExe && !BuildZip && !BuildMsix)
        {
            Status = "\u8bf7\u81f3\u5c11\u9009\u62e9\u4e00\u79cd\u8f93\u51fa\u683c\u5f0f";
            return;
        }

        IsBuilding = true;
        BuildCommand.NotifyCanExecuteChanged();
        Log = "";
        Progress = 0;
        try
        {
            if (BuildMsix)
                await Task.Run(() => _service.BuildMsixAsync(Version.Trim(), OutputPath.Trim()));
            if (BuildExe || BuildZip)
                await Task.Run(() => _service.BuildExeAsync(Version.Trim(), Configuration, OutputPath.Trim(), BuildExe, BuildZip));
            FlushPendingLogs();
            Progress = 100;
            Status = "\u6784\u5efa\u5b8c\u6210";
            Log += Environment.NewLine + "\u2713 \u5168\u90e8\u5b8c\u6210" + Environment.NewLine;
        }
        catch (Exception ex)
        {
            Status = "\u6784\u5efa\u5931\u8d25";
            Log += Environment.NewLine + "\u2717 " + ex.Message + Environment.NewLine;
        }
        finally
        {
            IsBuilding = false;
            BuildCommand.NotifyCanExecuteChanged();
        }
    }

    private void FlushPendingLogs()
    {
        if (_pendingLogs.IsEmpty) return;

        var batch = new System.Text.StringBuilder();
        while (_pendingLogs.TryDequeue(out string? line))
            batch.AppendLine(line);

        Log += batch.ToString();
    }

    [RelayCommand]
    private void OpenOutput()
    {
        string path = Path.GetFullPath(OutputPath.Trim());
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private static string FindRepositoryRoot(string start)
    {
        foreach (string candidate in new[] { start, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(candidate);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Haiyu repository root was not found.");
    }
}
