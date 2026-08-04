using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Haiyu.Publish.Services;

public sealed class ReleaseBuildService
{
    private readonly string _repositoryRoot;
    public event Action<string>? LogReceived;
    public event Action<double, string>? ProgressChanged;

    public ReleaseBuildService(string repositoryRoot) => _repositoryRoot = repositoryRoot;

    public string ReadCurrentVersion()
    {
        try
        {
            var document = XDocument.Load(Path.Combine(_repositoryRoot, "src", "Setup", "Project.WPFSetup", "Resources", "Resource1.resx"));
            return document.Descendants("data").First(x => (string?)x.Attribute("name") == "Version").Element("value")?.Value ?? "1.0.0";
        }
        catch { return "1.0.0"; }
    }

    public async Task BuildMsixAsync(string version, string output)
    {
        string packageVersion = NormalizePackageVersion(version);
        string project = Path.Combine(_repositoryRoot, "src", "WutheringWavesTool", "Haiyu.csproj");
        string appCode = Path.Combine(_repositoryRoot, "src", "WutheringWavesTool", "App.xaml.cs");
        string manifest = Path.Combine(_repositoryRoot, "src", "WutheringWavesTool", "Package.appxmanifest");
        string packageOutput = Path.Combine(Path.GetFullPath(output), "msix");

        Directory.CreateDirectory(packageOutput);
        UpdateAppVersion(appCode, version);
        UpdateManifestVersion(manifest, packageVersion);
        SetStep("MSIX 1/2  更新商店版本号", 10);
        AppendLog($"Package.appxmanifest 版本：{packageVersion}");

        SetStep("MSIX 2/2  生成 Microsoft Store 上传包", 25);
        string msbuild = ResolveMsBuildPath();
        string outDir = Path.GetFullPath(packageOutput).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string appxPackageDir = outDir.Replace('\\', '/') + "/";
        await RunAsync(msbuild,
            $"\"{project}\" /restore /t:Publish /v:minimal " +
            "/p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 " +
            "/p:WindowsPackageType=MSIX /p:GenerateAppxPackageOnBuild=true " +
            "/p:AppxBundle=Always /p:AppxBundlePlatforms=x64 " +
            "/p:UapAppxPackageBuildMode=StoreUpload /p:AppxPackageSigningEnabled=true " +
            $"/p:AppxPackageDir=\"{appxPackageDir}\"");

        string? bundle = Directory.EnumerateFiles(packageOutput, "*.msixbundle", SearchOption.AllDirectories).FirstOrDefault();
        if (bundle == null) throw new FileNotFoundException($"MSIX 构建结束，但输出目录中没有捆绑包：{packageOutput}");

        string finalBundle = Path.Combine(packageOutput, $"Haiyu_{packageVersion}_x64.msixbundle");
        if (!string.Equals(bundle, finalBundle, StringComparison.OrdinalIgnoreCase))
            File.Copy(bundle, finalBundle, true);

        string? storeUpload = Directory.EnumerateFiles(packageOutput, "*.msixupload", SearchOption.AllDirectories).FirstOrDefault();
        CleanupMsixIntermediates(packageOutput);
        SetStep("MSIX 捆绑包生成完成", 100);
        AppendLog($"捆绑包：{finalBundle}");
        if (storeUpload != null) AppendLog($"Microsoft Store 上传包：{storeUpload}");
    }

    public async Task BuildExeAsync(string version, string configuration, string output, bool buildInstaller, bool exportZip)
    {
        string appProject = Path.Combine(_repositoryRoot, "src", "WutheringWavesTool", "Haiyu.csproj");
        string appCode = Path.Combine(_repositoryRoot, "src", "WutheringWavesTool", "App.xaml.cs");
        string setupProject = Path.Combine(_repositoryRoot, "src", "Setup", "Project.WPFSetup", "Project.WPFSetup.csproj");
        string resources = Path.Combine(Path.GetDirectoryName(setupProject)!, "Resources");
        string resx = Path.Combine(resources, "Resource1.resx");
        string work = Path.Combine(_repositoryRoot, "artifacts", "temp", version);
        string appPublish = Path.Combine(work, "app");
        string setupOutput = Path.Combine(work, "setup");
        output = Path.GetFullPath(output);

        Directory.CreateDirectory(resources);
        RecreateDirectory(work);
        Directory.CreateDirectory(output);

        SetStep("1/5  发布 Haiyu 主程序", 8);
        UpdateAppVersion(appCode, version);
        AppendLog($"已更新 Haiyu App.xaml.cs 版本：{version}");
        await RunAsync("dotnet", $"publish \"{appProject}\" -c {configuration} -r win-x64 --self-contained true -p:WindowsPackageType=None -p:PublishProfile=win-x64 -o \"{appPublish}\"");
        EnsureFile(Path.Combine(appPublish, "Haiyu.exe"), "主程序发布结果");

        int removedPdbCount = DeleteFiles(appPublish, "*.pdb");
        AppendLog($"已清理 {removedPdbCount} 个 PDB 调试文件");

        SetStep("2/5  压缩安装文件", 36);
        string programZip = Path.Combine(resources, "program.zip");
        if (File.Exists(programZip)) File.Delete(programZip);
        await Task.Run(() => ZipFile.CreateFromDirectory(appPublish, programZip, CompressionLevel.Optimal, false));
        AppendLog($"已更新 Resources\\program.zip ({new FileInfo(programZip).Length / 1024d / 1024d:F1} MB)");

        if (exportZip)
        {
            string zipOutput = Path.Combine(output, $"Haiyu_{version}_win-x64.zip");
            File.Copy(programZip, zipOutput, true);
            AppendLog($"ZIP 输出：{zipOutput}");
        }

        if (!buildInstaller)
        {
            SetStep("ZIP 免安装包生成完成", 100);
            return;
        }

        SetStep("3/5  写入版本号", 55);
        UpdateResxVersion(resx, version);
        AppendLog($"安装程序版本：{version}");

        // 第一遍使用空载荷产生轻量的卸载程序；第二遍再嵌入真实程序和该卸载程序。
        SetStep("4/5  生成卸载程序", 63);
        await BuildLightweightUninstallerAsync(setupProject, setupOutput, resx, work);
        string setupExe = Path.Combine(setupOutput, "Project.WPFSetup.exe");
        EnsureFile(setupExe, "安装程序第一遍构建结果");
        File.Copy(setupExe, Path.Combine(resources, "uninstall.exe"), true);
        AppendLog("已更新 Resources\\uninstall.exe");

        SetStep("5/5  生成最终 EXE 安装包", 80);
        RecreateDirectory(setupOutput);
        await BuildSetupAsync(setupProject, configuration, setupOutput);
        EnsureFile(setupExe, "最终安装程序");
        string finalExe = Path.Combine(output, $"Haiyu-{version}-win-x64.exe");
        File.Copy(setupExe, finalExe, true);
        AppendLog($"输出：{finalExe}");
        AppendLog($"大小：{new FileInfo(finalExe).Length / 1024d / 1024d:F1} MB");
    }

    private Task BuildSetupAsync(string project, string configuration, string output)
    {
        // 安装项目含 IWshRuntimeLibrary COM 引用，需使用 Visual Studio 的完整 MSBuild，
        // dotnet build 会在 ResolveComReference 阶段报 MSB4803。
        string msbuild = ResolveMsBuildPath();
        // Do not put a trailing backslash immediately before the closing quote. On Windows
        // it escapes that quote and MSBuild receives an invalid path ending in `"\`.
        string outDir = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return RunAsync(msbuild, $"\"{project}\" /t:Rebuild /m /v:minimal /p:Configuration={configuration} /p:Platform=x64 /p:OutDir=\"{outDir}\"");
    }

    private async Task BuildLightweightUninstallerAsync(
        string project, string output, string resx, string work)
    {
        string savedResx = Path.Combine(work, "Resource1.full.resx");
        File.Copy(resx, savedResx, true);

        try
        {
            // Both binary resource references point at the existing zero-byte text file.
            // This keeps the uninstaller build small and avoids recursively embedding an EXE.
            var document = XDocument.Load(resx, LoadOptions.PreserveWhitespace);
            foreach (string name in new[] { "InstallFile", "Unstaller" })
            {
                var value = document.Descendants("data")
                    .First(x => (string?)x.Attribute("name") == name)
                    .Element("value") ?? throw new InvalidDataException($"Resource1.resx 中缺少 {name} 项。");
                value.Value = "Simple.txt;System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
            }
            document.Save(resx, SaveOptions.DisableFormatting);

            AppendLog("卸载程序资源已临时替换为空 TXT 文件");
            await BuildSetupAsync(project, "Release", output);
        }
        finally
        {
            File.Copy(savedResx, resx, true);
        }
    }

    private async Task RunAsync(string fileName, string arguments)
    {
        AppendLog($"> {fileName} {arguments}");
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, a) => { if (a.Data != null) AppendLog(a.Data); };
        process.ErrorDataReceived += (_, a) => { if (a.Data != null) AppendLog(a.Data); };
        if (!process.Start()) throw new InvalidOperationException($"启动 {fileName} 失败。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"命令执行失败，退出代码 {process.ExitCode}。请查看上方日志。");
    }

    private void SetStep(string text, double progress)
    {
        ProgressChanged?.Invoke(progress, text);
        AppendLog($"\n=== {text} ===");
    }

    private void AppendLog(string text) => LogReceived?.Invoke(text);

    private static void UpdateResxVersion(string path, string version)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var value = document.Descendants("data").FirstOrDefault(x => (string?)x.Attribute("name") == "Version")?.Element("value")
            ?? throw new InvalidDataException("Resource1.resx 中缺少 Version 项。");
        value.Value = version;
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static void UpdateAppVersion(string path, string version)
    {
        string source = File.ReadAllText(path, Encoding.UTF8);
        const string pattern = "public\\s+static\\s+string\\s+AppVersion\\s*=>\\s*\"[^\"]+\"\\s*;";
        string updated = Regex.Replace(
            source,
            pattern,
            $"public static string AppVersion => \"{version}\";",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        if (ReferenceEquals(source, updated) || source == updated)
        {
            if (!source.Contains($"AppVersion => \"{version}\"", StringComparison.Ordinal))
                throw new InvalidDataException("Haiyu App.xaml.cs 中未找到 AppVersion 定义。");
            return;
        }

        File.WriteAllText(path, updated, new UTF8Encoding(false));
    }

    private static void UpdateManifestVersion(string path, string version)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var identity = document.Root?.Element(ns + "Identity")
            ?? throw new InvalidDataException("Package.appxmanifest 中缺少 Identity 节点。");
        identity.SetAttributeValue("Version", version);
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static string NormalizePackageVersion(string version)
    {
        string[] parts = version.Split('.');
        if (parts.Length == 3) return version + ".0";
        if (parts.Length == 4) return version;
        throw new FormatException("MSIX 版本号必须是 3 段或 4 段数字。");
    }

    private static void CleanupMsixIntermediates(string packageOutput)
    {
        foreach (string directory in Directory.EnumerateDirectories(packageOutput, "*_Test", SearchOption.TopDirectoryOnly))
            Directory.Delete(directory, true);
    }

    private static void EnsureFile(string path, string description)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"未找到{description}：{path}");
    }

    private static int DeleteFiles(string root, string pattern)
    {
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
        {
            File.Delete(file);
            count++;
        }
        return count;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
            directory = directory.Parent;
        }
        // 开发时输出目录较深，当前工作目录通常就是仓库根目录。
        directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("未找到 Haiyu 仓库根目录。");
    }

    private static string ResolveMsBuildPath()
    {
        string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string vswhere = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (File.Exists(vswhere))
        {
            var info = new ProcessStartInfo(vswhere, "-latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(info);
            string? path = process?.StandardOutput.ReadLine();
            process?.WaitForExit();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        }

        throw new FileNotFoundException("未检测到 Visual Studio MSBuild。请在 Visual Studio Installer 中安装“.NET 桌面开发”组件。");
    }

}
