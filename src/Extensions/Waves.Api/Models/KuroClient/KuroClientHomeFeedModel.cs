using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient;

public class CoverImage
{
    [JsonPropertyName("imgHeight")]
    public int ImgHeight { get; set; }

    [JsonPropertyName("imgWidth")]
    public int ImgWidth { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("isAbnormal")]
    public bool IsAbnormal { get; set; }

    [JsonPropertyName("pointOffsetX")]
    public int PointOffsetX { get; set; }

    [JsonPropertyName("pointOffsetY")]
    public int PointOffsetY { get; set; }

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class KuroClientHomeFeedModel
{
    [JsonPropertyName("group")]
    public string Group { get; set; }

    [JsonPropertyName("hasNext")]
    public int HasNext { get; set; }

    [JsonPropertyName("postList")]
    public List<PostList> PostList { get; set; }

    [JsonPropertyName("recommendId")]
    public string RecommendId { get; set; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; }

    [JsonPropertyName("scm")]
    public string Scm { get; set; }

    [JsonPropertyName("styleType")]
    public int StyleType { get; set; }

    [JsonPropertyName("topList")]
    public List<object> TopList { get; set; }
}

public class ImgContent
{
    [JsonPropertyName("imgHeight")]
    public int ImgHeight { get; set; }

    [JsonPropertyName("imgWidth")]
    public int ImgWidth { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class PostList
{
    [JsonPropertyName("browseCount")]
    public string BrowseCount { get; set; }

    [JsonPropertyName("commentCount")]
    public int CommentCount { get; set; }

    [JsonPropertyName("coverImages")]
    public List<CoverImage> CoverImages { get; set; }

    [JsonPropertyName("createTimestamp")]
    public string CreateTimestamp { get; set; }

    [JsonPropertyName("gameForumId")]
    public int GameForumId { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("gameName")]
    public string GameName { get; set; }

    [JsonPropertyName("imgContent")]
    public List<ImgContent> ImgContent { get; set; }

    [JsonPropertyName("imgCount")]
    public int ImgCount { get; set; }

    [JsonPropertyName("ipRegion")]
    public string IpRegion { get; set; }

    [JsonPropertyName("isFollow")]
    public int IsFollow { get; set; }

    [JsonPropertyName("isLike")]
    public int IsLike { get; set; }

    [JsonPropertyName("isLock")]
    public int IsLock { get; set; }

    [JsonPropertyName("isPublisher")]
    public int IsPublisher { get; set; }

    [JsonPropertyName("lastEditIpRegion")]
    public string LastEditIpRegion { get; set; }

    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }

    [JsonPropertyName("postContent")]
    public string PostContent { get; set; }

    [JsonPropertyName("postId")]
    public string PostId { get; set; }

    [JsonPropertyName("postTitle")]
    public string PostTitle { get; set; }

    [JsonPropertyName("postType")]
    public int PostType { get; set; }

    [JsonPropertyName("reviewStatus")]
    public int ReviewStatus { get; set; }

    [JsonPropertyName("showTime")]
    public string ShowTime { get; set; }

    [JsonPropertyName("topicList")]
    public List<TopicList> TopicList { get; set; }

    [JsonPropertyName("userHeadUrl")]
    public string UserHeadUrl { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; }

    [JsonPropertyName("userLevel")]
    public int UserLevel { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; }

    [JsonPropertyName("identifyClassify")]
    public int? IdentifyClassify { get; set; }

    [JsonPropertyName("identifyNames")]
    public string IdentifyNames { get; set; }

    [JsonPropertyName("newIdentifyNames")]
    public List<string> NewIdentifyNames { get; set; }

    [JsonPropertyName("videoId")]
    public string VideoId { get; set; }
}


public class TopicList
{
    [JsonPropertyName("postId")]
    public string PostId { get; set; }

    [JsonPropertyName("topicId")]
    public int TopicId { get; set; }

    [JsonPropertyName("topicName")]
    public string TopicName { get; set; }
}
