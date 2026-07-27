using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Waves.Api.Models.GameWikiiClient;
using WinUIEx.Messaging;

namespace Haiyu.Models.Wrapper.Wiki
{
    public partial class PunishEveryweekContentWrapper : ObservableObject
    {
        [ObservableProperty]
        public partial string NormanRemainingTime { get; set; }
        [ObservableProperty]
        public partial double NormanMaxProgress { get; set; }

        [ObservableProperty]
        public partial double NormanProgress { get; set; }

        [ObservableProperty]
        public partial string NormanImageUrl { get; set; }

        [ObservableProperty]
        public partial string WarZoneRemainingTime { get; set; }

        [ObservableProperty]
        public partial double WarZoneProgress { get; set; }
        [ObservableProperty]
        public partial double WarZoneMaxProgress { get; set; }

        [ObservableProperty]
        public partial string WarZoneImageUrl { get; set; }

        [ObservableProperty]
        public partial string WarZoneBuffName { get; set; }

        [ObservableProperty]
        public partial string WarZoneBuffDescription { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<DisputeWarZoneItem> DisputeWarZoneItems { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<ImageUrlItem> PunishCageItems { get; set; } = new();

        [ObservableProperty]
        public partial string PunishRemainingTime { get; set; }

        [ObservableProperty]
        public partial double PunishCageProgress { get; set; } = 50;
        [ObservableProperty]
        public partial double PunishCageMaxProgress { get; set; } = 100;


        private Dictionary<string, PunishCageCacheItem> _punishCageCache = new();


        public void Dispose()
        {

            this.NormanMaxProgress = 0;
            this.NormanProgress = 0;
            this.NormanImageUrl = null;
            this.WarZoneRemainingTime = null;
            this.WarZoneProgress = 0;
            this.WarZoneMaxProgress = 0;
            this.WarZoneImageUrl = null;
            this.WarZoneBuffName = null;
            this.WarZoneBuffDescription = null;

            DisputeWarZoneItems.Clear();
            PunishCageItems.Clear();
            _punishCageCache.Clear();
            NormanRemainingTime = null;
        }

        public void InitWeekContent(List<SideModule> modules, List<MainModule> mainModules = null)
        {
            SideModule? norman = null;
            SideModule? warzone = null;
            SideModule? cage = null;
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module.Title == LanguageService.GetStringByText("诺曼复兴战")) norman = module;
                    if (module.Title == LanguageService.GetStringByText("历战映射")) warzone = module;
                    if (module.Title == LanguageService.GetStringByText("幻痛囚笼")) cage = module;
                }
            }

            if (norman != null) SetData(ref norman, SetNormanDataInternal, null);
            if (warzone != null) SetData(ref warzone, SetWarZoneDataInternal, null, true);
            if (cage != null) SetData(ref cage, SetPunishCageDataInternal, SetPunishCageData, true);

            if (mainModules != null)
            {
                foreach (var module in mainModules)
                {
                    if (module.Title == LanguageService.GetStringByText("纷争战区"))
                    {
                        SetDisputeDataFromMain(module);
                        break;
                    }
                }
            }
            UpdatePunishCageContent();
        }





        private (double currVal, double maxVal, string msg) SetDataDisplay(SideModuleContentTab item, double currVal, double maxVal, string msg, int days = 14)
        {
            if (item == null) return (0, 0, null);


            if (item.CountDown != null && item.CountDown.Repeat != null && item.CountDown.Repeat.DataRanges != null)
            {
                var dataRange = item.CountDown.Repeat.DataRanges.FirstOrDefault();
                if (dataRange != null && dataRange.DataRange != null && dataRange.DataRange.Count >= 2)
                {

                    if (DateTime.TryParse(dataRange.DataRange[0], out var start) &&
                        DateTime.TryParse(dataRange.DataRange[1], out var end))
                    {
                        var res = CalculateLatestDayCycle(start.ToString(), days);
                        start = res[0];
                        end = res[1];
                        var now = DateTime.Now;
                        TimeSpan _endCountdownTimeSpan = end - now;
                        TimeSpan _totalDurationTimeSpan = end - start;
                        TimeSpan _overCountdownTimeSpace = now - start;
                        maxVal = _totalDurationTimeSpan.TotalSeconds;
                        double elapsed = _endCountdownTimeSpan.TotalSeconds;

                        if (elapsed <= 0)
                        {
                            currVal = maxVal;
                            msg = LanguageService.GetStringByText("已结束");
                            return (currVal, maxVal, msg);
                        }



                        currVal = _overCountdownTimeSpace.TotalSeconds;
                        msg = LanguageService.FormatByText(LanguageService.GetStringByText("剩余{0}天"), _endCountdownTimeSpan.Days) +
                                    LanguageService.FormatByText(LanguageService.GetStringByText("{0}小时"), _endCountdownTimeSpan.Hours) +
                                    LanguageService.FormatByText(LanguageService.GetStringByText("{0}分"), _endCountdownTimeSpan.Minutes);
                        return (currVal, maxVal, msg);
                    }
                    return (0, 0, string.Empty);
                }
            }
            return (0, 0, string.Empty);
        }

        private void SetWarZoneDataInternal(SideModuleContentTab item)
        {
            if (item == null) return;

            if (item.Imgs != null && item.Imgs.Count > 0)
            {
                WarZoneImageUrl = item.Imgs[0].Img;
            }

            if (item.InnerTabs != null && item.InnerTabs.Count > 0)
            {
                var buff = item.InnerTabs.FirstOrDefault();
                if (buff != null)
                {
                    WarZoneBuffName = buff.Name;
                    WarZoneBuffDescription = buff.Description;
                }
            }



            var result = SetDataDisplay(item, WarZoneProgress, WarZoneMaxProgress, WarZoneRemainingTime);
            WarZoneMaxProgress = result.maxVal;
            WarZoneProgress = result.currVal;
            WarZoneRemainingTime = result.msg;
        }


        private void SetNormanDataInternal(SideModuleContentTab item)
        {

            if (item == null) return;

            if (item.Imgs != null && item.Imgs.Count > 0)
            {
                NormanImageUrl = item.Imgs[0].Img;
            }

            var result = SetDataDisplay(item, NormanProgress, NormanMaxProgress, NormanRemainingTime);
            NormanMaxProgress = (double)result.maxVal;
            NormanProgress = (double)result.currVal;
            NormanRemainingTime = result.msg;
        }

        private void SetPunishCageDataInternal(SideModuleContentTab item)
        {
            if (item == null) return;


            var result = SetDataDisplay(item, PunishCageProgress, PunishCageMaxProgress, PunishRemainingTime, 7);
            PunishCageMaxProgress = result.maxVal;
            PunishCageProgress = result.currVal;
            PunishRemainingTime = result.msg;
        }
        public void SetDisputeDataFromMain(MainModule module)
        {
            if (module == null) return;
            if (module.Content is JsonElement element)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        TypeInfoResolver = WikiContext.Default
                    };

                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        try
                        {
                            var items = element.Deserialize<List<DisputeJsonItem>>(options);
                            if (items != null && items.Count > 0)
                            {
                                DisputeWarZoneItems.Clear();
                                foreach (var item in items)
                                {
                                    var disputeItem = new DisputeWarZoneItem();
                                    disputeItem.Title = item.Title;
                                    disputeItem.ImageUrl = item.ContentUrl;

                                    if (item.CountDown != null)
                                    {
                                        List<string> range = null;
                                        if (item.CountDown.DateRange != null && item.CountDown.DateRange.Count >= 2)
                                        {
                                            range = item.CountDown.DateRange;
                                        }
                                        else if (item.CountDown.Repeat != null && item.CountDown.Repeat.DataRanges != null)
                                        {
                                            var dr = item.CountDown.Repeat.DataRanges.FirstOrDefault();
                                            if (dr != null) range = dr.DataRange;
                                        }

                                        if (range != null && range.Count >= 2)
                                        {
                                            if (DateTime.TryParse(range[0], out var start) && DateTime.TryParse(range[1], out var end))
                                            {
                                                disputeItem.TimeRange = $"{start:MM.dd HH:mm}-{end:MM.dd HH:mm}";
                                                var now = DateTime.Now;
                                                TimeSpan _endCountdownTimeSpan = end - now;
                                                TimeSpan _totalDurationTimeSpan = end - start;
                                                TimeSpan _overCountdownTimeSpace = now - start;
                                                disputeItem.MaxProgress = _totalDurationTimeSpan.TotalSeconds;
                                                double elapsed = _endCountdownTimeSpan.TotalSeconds;


                                                if (elapsed <= 0)
                                                {
                                                    disputeItem.CurrentProgress = disputeItem.MaxProgress;
                                                    disputeItem.RemainingTime = LanguageService.GetStringByText("已结束");
                                                    disputeItem.ColorVal = "Red";
                                                    return;
                                                }

                                                disputeItem.CurrentProgress = _overCountdownTimeSpace.TotalSeconds;
                                                disputeItem.RemainingTime = LanguageService.FormatByText(LanguageService.GetStringByText("剩余{0}天"), _endCountdownTimeSpan.Days) +
                                                            LanguageService.FormatByText(LanguageService.GetStringByText("{0}小时"), _endCountdownTimeSpan.Hours) +
                                                            LanguageService.FormatByText(LanguageService.GetStringByText("{0}分"), _endCountdownTimeSpan.Minutes);
                                                disputeItem.ColorVal = "#66CCFF";


                                            }
                                        }
                                    }
                                    DisputeWarZoneItems.Add(disputeItem);
                                }
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error parsing DisputeJsonItem: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing Dispute data: {ex.Message}");
                }
            }
        }
        public void SetPunishCageData(SideModuleContentJson content)
        {
            var items = new List<ImageUrlItem>();
            foreach (var tab in content.Tabs)
            {

                if (tab.Imgs != null)
                {
                    foreach (var img in tab.Imgs)
                    {
                        items.Add(new ImageUrlItem
                        {
                            ImageUrl = img.Img
                        });
                    }
                }
                if (!string.IsNullOrEmpty(tab.Name))
                {
                    var key = tab.Name.Trim();
                    var cacheItem = new PunishCageCacheItem { Items = items };

                    _punishCageCache[key] = cacheItem;
                }
            }

        }


        private void SetData(ref SideModule module, Action<SideModuleContentTab> callback, Action<SideModuleContentJson> call_2 = null, bool isOptions = false)
        {

            if (module.Content is JsonElement element)
            {
                JsonSerializerOptions options = null;
                try
                {
                    if (isOptions)
                    {
                        options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            TypeInfoResolver = WikiContext.Default
                        };
                    }
                    SideModuleContentJson? content = null;

                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        List<SideModuleContentJson> list = null;
                        if (isOptions) list = element.Deserialize<List<SideModuleContentJson>>(options);
                        else list = JsonSerializer.Deserialize(element, WikiContext.Default.ListSideModuleContentJson);

                        content = list?.FirstOrDefault();
                    }
                    else
                    {
                        if (isOptions) content = element.Deserialize<SideModuleContentJson>(options);
                        else content = JsonSerializer.Deserialize(element, WikiContext.Default.SideModuleContentJson);
                    }

                    if (content != null && content.Tabs != null && content.Tabs.Count > 0)
                    {
                        var tab = content.Tabs.FirstOrDefault(t => (bool)t.Active) ?? content.Tabs.First();
                        callback(tab);
                        if (call_2 != null) call_2(content);
                    }



                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing Norman data: {ex.Message}");
                }
            }
            return;
        }




        private void FillDisputeItems(List<SideModuleContentTab> tabs)
        {

            foreach (var tab in tabs)
            {
                var item = new DisputeWarZoneItem();
                item.Title = tab.Name;
                if (tab.Imgs != null && tab.Imgs.Count > 0)
                {
                    item.ImageUrl = tab.Imgs[0].Img;
                }

                if (tab.CountDown != null)
                {
                    
                    List<string> range = null;
                    if (tab.CountDown.DateRange != null && tab.CountDown.DateRange.Count >= 2)
                    {
                        range = tab.CountDown.DateRange;
                    }
                    else if (tab.CountDown.Repeat != null && tab.CountDown.Repeat.DataRanges != null)
                    {
                        var dr = tab.CountDown.Repeat.DataRanges.FirstOrDefault();
                        if (dr != null) range = dr.DataRange;
                    }

                    if (range != null && range.Count >= 2)
                    {
                        if (DateTime.TryParse(range[0], out var start) && DateTime.TryParse(range[1], out var end))
                        {
                            item.TimeRange = $"{start:MM.dd HH:mm}-{end:MM.dd HH:mm}";

                        }
                    }
                }
                DisputeWarZoneItems.Add(item);
            }
        }
        public void UpdatePunishCageContent()
        {
            var key = LanguageService.GetStringByText("高级区");
            if (_punishCageCache.ContainsKey(key))
            {
                var cacheItem = _punishCageCache[key];
                PunishCageItems.Clear();
                foreach (var item in cacheItem.Items)
                {
                    PunishCageItems.Add(item);
                }
                PunishCageProgress = cacheItem.Progress;
            }
        }
        /// <summary>
        /// 计算活动周期
        /// </summary>
        /// <param name="startDateString"></param>
        /// <param name="days"></param>
        /// <returns></returns>
        public DateTime[] CalculateLatestDayCycle(string startDateString, int days = 7)
        {
            DateTime startDate = DateTime.Parse(startDateString);


            DateTime now = DateTime.Now;
            TimeSpan totalTimeSpan = now - startDate;
            double totalDays = totalTimeSpan.TotalDays;


            int completeCycles = (int)(totalDays / days);



            DateTime latestFutureDate = startDate.AddDays((completeCycles + 1) * days);
            DateTime latestPastDate = latestFutureDate.AddDays(-days);

            // 返回数组
            return new DateTime[] { latestPastDate, latestFutureDate };
        }
    }



    public class PunishCageCacheItem
    {
        public List<ImageUrlItem> Items { get; set; } = new();
        public string? Status { get; set; }
        public double Progress { get; set; }
    }

    public class ImageUrlItem
    {
        public string? ImageUrl { get; set; }
    }

    public class DisputeWarZoneItem
    {
        public string? Title { get; set; }
        public string? ImageUrl { get; set; }
        public string? TimeRange { get; set; }
        public double CurrentProgress { get; set; }
        public double MaxProgress { get; set; }
        public string? RemainingTime { get; set; }
        public string? ColorVal { get; set; }
    }
}
