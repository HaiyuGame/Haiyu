using Project.WPFSetup.Resources;

namespace Project.WPFSetup.Common.Setups;

public class RemoveRegisterSetup : ISetup
{
    public string SetupName => "删除注册信息";

    public int MaxProgress => 1;

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
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(
                    $"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{property.ProductId}"
                );
                return ("", true);
            }
            catch (Exception ex)
            {
                progress.Report((0, "失败"));
                return (ex.Message, false);
            }
        });
    }
}
