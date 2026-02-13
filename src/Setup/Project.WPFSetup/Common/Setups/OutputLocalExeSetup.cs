using System.IO;

namespace Project.WPFSetup.Common.Setups;

public class OutputLocalExeSetup : ISetup
{
    /// <summary>
    /// 输出卸载程序
    /// </summary>
    public OutputLocalExeSetup() { }

    public string SetupName => "复制程序";

    public int MaxProgress => 100;

    public async Task<(string, bool)> ExecuteAsync(
        SetupProperty property,
        IProgress<(double, string)> progress,
        int maxValue
    )
    {
        return await Task.Run(() =>
        {
            try
            {
                string sourcePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string destinationPath = property.GetUninstallPath();
                property.UninstallString = $"{destinationPath}";
                progress.Report((100, "成功"));
                return ("", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file: {ex.Message}");
                return (ex.Message, false);
            }
        });
    }
}
