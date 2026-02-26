using Haiyu.Models.Wrapper.Wiki;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waves.Api.Models.GameWikiiClient;

namespace Haiyu.Helpers;

public static class WikiExtensions
{
    extension(IEnumerable<HotContentSide> source)
    {
        public ObservableCollection<HotContentSideWrapper>? Format()
        {
            ObservableCollection<HotContentSideWrapper> wrappers = new();
            if (source == null)
                return wrappers;
            foreach (var item in source)
            {
                var value = new HotContentSideWrapper()
                {
                    Title = item.Title,
                    ImageUrl = item.ContentUrl,
                    StartTime = item.CountDown == null ? DateTime.Now.ToString() : item.CountDown.DateRange[0],
                    EndTime = item.CountDown == null ? DateTime.Now.AddYears(1).ToString() : item.CountDown.DateRange[1],
                    JumpUrl = item.LinkConfig.LinkUrl
                };
                if (item.CountDown != null)
                {
                    var spanResult = (DateTime.Parse(item.CountDown.DateRange[1]) - DateTime.Now);
                }
                value.Cali();
                wrappers.Add(value);
            }
            return wrappers;
        }
    }
}
