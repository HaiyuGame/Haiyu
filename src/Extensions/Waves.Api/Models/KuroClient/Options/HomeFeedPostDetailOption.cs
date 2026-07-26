using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Api.Models.KuroClient.Options;

public class HomeFeedPostDetailOption
{
    public string IsOnlyPublisher { get; set; }

    public string PostId { get; set; }
    public string ShowOrderType { get; set; }

    public virtual Dictionary<string, string> ConvertParam()
    {
        return new Dictionary<string, string>
        {
            { "isOnlyPublisher", IsOnlyPublisher },
            { "postId", PostId },
            { "ShowOrderType", ShowOrderType },
        };
    }

    public static HomeFeedPostDetailOption Create(string postId)
    {
        return new()
        {
            IsOnlyPublisher = "0",
            PostId = postId,
            ShowOrderType = "2",
        };
    }
}
