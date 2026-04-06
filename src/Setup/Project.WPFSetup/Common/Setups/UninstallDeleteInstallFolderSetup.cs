using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project.WPFSetup.Common.Setups
{
    public class UninstallDeleteInstallFolderSetup : ISetup
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
            var uninstallDat =Path.Combine(installFolder, "unstall.dat");
            if (File.Exists(uninstallDat))
            {
                var reader = File.OpenText(uninstallDat);
                var list = JsonSerializer.Deserialize<List<string>>(await reader.ReadToEndAsync());
                while (true)
                {
                    try
                    {
                        foreach (var file in list)
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                            }
                        }
                        break;
                    }
                    catch
                    {
                    }
                }
            }
            progress.Report((1, SetupName));
            return ("", true);
        }

    }
}
