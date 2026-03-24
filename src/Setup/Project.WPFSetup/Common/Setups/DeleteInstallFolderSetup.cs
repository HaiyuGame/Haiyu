using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Project.WPFSetup.Common.Setups;

public class DeleteInstallFolderSetup : ISetup
{
    public string SetupName => "删除旧文件";

    public int MaxProgress => 1;

    public async Task<(string, bool)> ExecuteAsync(
        SetupProperty property,
        IProgress<(double, string)> progress,
        int maxValue
    )
    {
        var installFolder = property.InstallPath;
        var result = await Remove(property, installFolder);
        progress.Report((1, SetupName));
        return ("", true);
    }

    public async Task<bool> Remove(SetupProperty setupProperty, string directoryPath)
    {
        try
        {
            return await Task.Run(async () =>
            {
                var installFolder = setupProperty.InstallPath;
                var uninstallDat = Path.Combine(installFolder, "unstall.dat");
                var unstallexe = Path.Combine(installFolder, "uninstall.exe");
                if (File.Exists(uninstallDat))
                {
                    var reader = File.OpenText(uninstallDat);
                    var list = JsonSerializer.Deserialize<List<string>>(await reader.ReadToEndAsync());
                    if (list == null)
                        return true;
                    try
                    {
                        foreach (var file in list)
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                            }
                        }
                    }
                    catch
                    {
                    }
                    reader.Dispose();
                }
                File.Delete(uninstallDat);
                File.Delete(unstallexe);
                return true;
            });
        }
        catch (Exception)
        {
            return false;
        }
        
    }
}
