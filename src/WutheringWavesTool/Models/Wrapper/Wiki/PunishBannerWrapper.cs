using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.GameWikiiClient;

namespace Haiyu.Models.Wrapper.Wiki
{
    public partial class PunishBannerWrapper : ObservableObject
    {

        private List<SideModule> bannerList = new List<SideModule>();
        [ObservableProperty]
        public partial ObservableCollection<PunishBannerWrapperItem> Items { get; set; } = new();
        public void InitBanner(List<SideModule> modules)
        {
            bannerList.Clear();
            foreach (var value in modules)
            {
                if (value != null && value.Title != null)
                {

                    if (value.Title.Contains(LanguageService.GetStringByText("池")))
                        bannerList.Add(value);
                }

            }

            Items.Clear();
            foreach (var x in bannerList)
            {


                var item = new PunishBannerWrapperItem();
                if (x.Title != null) item.Title = x.Title;

                if (x.Content is JsonElement element)
                {
                    var v = element.Deserialize(WikiContext.Default.SideModuleContentJson);

                    var tab = v.Tabs.Where(v => (bool)v.Active).FirstOrDefault();
                    var result = SetDataInternal(tab, item.CurrentProgress, item.MaxProgress, item.RemianTime);
                    item.ImageUrls = new();
                    item.ImageUrlCount = tab.Imgs.Count(v => (v.Img != ""));
                    item.ImageUrl = tab.Imgs[0].Img;
                    foreach (var img in tab.Imgs)
                    {
                        if (img.Img == "") continue;
                        item.ImageUrls.Add(img.Img);
                    }


                    item.RemianTime = result.msg;
                    item.MaxProgress = result.maxVal;
                    item.CurrentProgress = result.currVal;
                }


                Items.Add(item);
            }


        }

        public void Dispose()
        {
            bannerList.Clear();
            bannerList = null;

            foreach (var item in Items)
            {
                item.ImageUrls?.Clear();
            }
            Items.Clear();
        }

        private void SetItems(SideModule module, int level = 0)
        {
            if (module == null) return;
            var item = new PunishBannerWrapperItem();
            if (module.Title != null)
                item.Title = module.Title;

            if (module.Content is JsonElement element)
            {
                var v = element.Deserialize(WikiContext.Default.SideModuleContentJson);
                var tab = v.Tabs.Where(v => (bool)v.Active).FirstOrDefault();
                if (item.RemianTime == null) return;
                var result = SetDataInternal(tab, item.CurrentProgress, item.MaxProgress, item.RemianTime);

                item.ImageUrl = tab.Imgs[0].Img;
                item.RemianTime = result.msg;
                item.MaxProgress = result.maxVal;
                item.CurrentProgress = result.currVal;

            }


            Items.Add(item);
        }

        private (double currVal, double maxVal, string msg) SetDataInternal(SideModuleContentTab item, double currVal, double maxVal, string msg)
        {

            if (item.CountDown != null && item.CountDown.Repeat != null && item.CountDown.Repeat.DataRanges != null)
            {
                var dataRange = item.CountDown.DateRange;
                if (dataRange != null && dataRange != null && dataRange.Count >= 2)
                {

                    if (DateTime.TryParse(dataRange[0], out var start) &&
                        DateTime.TryParse(dataRange[1], out var end))
                    {
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
                        msg = DisplayTimeFormatter.FormatDuration(_endCountdownTimeSpan);
                        return (currVal, maxVal, msg);
                    }
                    return (0, 0, string.Empty);
                }
            }
            return (0, 0, string.Empty);
        }


    }

    public class PunishBannerWrapperItem
    {
        public string Title { get; set; } = LanguageService.GetStringByText("标题");
        public double CurrentProgress { get; set; } = 10;
        public double MaxProgress { get; set; } = 100;
        public string? RemianTime { get; set; } = "L";
        public string? ImageUrl { get; set; }
        public int ImageUrlCount { get; set; }
        public List<string>? ImageUrls { get; set; }

    }

}
