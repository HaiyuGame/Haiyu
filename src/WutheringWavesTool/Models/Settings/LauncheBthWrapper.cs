using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Settings;

public class LauncheBthWrapper
{
    public string Memory { get; set; }

    public string Display { get; set; }

    public static ObservableCollection<LauncheBthWrapper> CreateDefault()
    {
        return
        [
            new LauncheBthWrapper() { Display = LanguageService.GetStringByText("首页"), Memory = "Home" },
            new LauncheBthWrapper() { Display = LanguageService.GetStringByText("鸣潮"), Memory = "WutheringWaves" },
            new LauncheBthWrapper() { Display = LanguageService.GetStringByText("战双"), Memory = "PunishingGrayRaven" },
            new LauncheBthWrapper() { Display = LanguageService.GetStringByText("云鸣潮"), Memory = "CloudWutheringWaves" },
        ];
    }
}
