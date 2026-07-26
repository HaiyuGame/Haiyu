using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Api.Models.KuroClient.Options
{
    public class HomeFeedOption
    {
        public string ForumId { get; set; }
        public string GameId { get; set; }
        public string PageIndex { get; set; }
        public string PageSize { get; set; }
        public string SearchType { get; set; }
        public string TimeType { get; set; }
        public string TopicId { get; set; }

        public virtual Dictionary<string,string> ConvertParam()
        {
            return new Dictionary<string, string>
            {
                {"forumId",ForumId },
                {"gameId",GameId },
                {"pageIndex",PageIndex },
                {"pageSize",PageSize },
                {"searchType",SearchType },
                {"timeType",TimeType },
                {"TopicId",TopicId },
            };
        }

        public static HomeFeedOption CreateHomeWaves(int pageIndex, int pageSize)
        {
            return new()
            {
                ForumId = "9",
                GameId = "3",
                PageIndex = pageIndex.ToString(),
                PageSize = pageSize.ToString(),
                SearchType = "3",
                TimeType = "0",
                TopicId = "0"
            };
        }

        public static HomeFeedOption CreateHomePunish(int pageIndex, int pageSize)
        {
            return new()
            {
                ForumId = "2",
                GameId = "2",
                PageIndex = pageIndex.ToString(),
                PageSize = pageSize.ToString(),
                SearchType = "3",
                TimeType = "0",
                TopicId = "0"
            };
        }
    }
}
