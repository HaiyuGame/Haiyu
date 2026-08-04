using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient;

public class KuroClientPostDetailChild
{
    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }
}

public class ContentLink
{
    [JsonPropertyName("isCustomTitle")]
    public bool IsCustomTitle { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class KuroClientPostPageDetail
{
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("isCollect")]
    public int IsCollect { get; set; }

    [JsonPropertyName("isFollow")]
    public int IsFollow { get; set; }

    [JsonPropertyName("isLike")]
    public int IsLike { get; set; }

    [JsonPropertyName("postDetail")]
    public PostDetail PostDetail { get; set; }
}

public class GameForumVo
{
    [JsonPropertyName("filterOfficalUserIds")]
    public string FilterOfficalUserIds { get; set; }

    [JsonPropertyName("forumDataType")]
    public int ForumDataType { get; set; }

    [JsonPropertyName("forumListShowType")]
    public int ForumListShowType { get; set; }

    [JsonPropertyName("forumType")]
    public int ForumType { get; set; }

    [JsonPropertyName("forumUiType")]
    public int ForumUiType { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("isOfficial")]
    public int IsOfficial { get; set; }

    [JsonPropertyName("isSpecial")]
    public int IsSpecial { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("rangeDay")]
    public int RangeDay { get; set; }

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}

public class PostContent
{
    [JsonPropertyName("contentType")]
    public int ContentType { get; set; }

    [JsonPropertyName("imgHeight")]
    public int ImgHeight { get; set; }

    [JsonPropertyName("imgWidth")]
    public int ImgWidth { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("children")]
    public List<KuroClientPostDetailChild> Children { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("contentLink")]
    public ContentLink ContentLink { get; set; }
}

public class PostDetail
{
    [JsonPropertyName("appealing")]
    public bool Appealing { get; set; }

    [JsonPropertyName("browseCount")]
    public string BrowseCount { get; set; }

    [JsonPropertyName("collectionCount")]
    public int CollectionCount { get; set; }

    [JsonPropertyName("commentCount")]
    public int CommentCount { get; set; }

    [JsonPropertyName("companyEventType")]
    public int CompanyEventType { get; set; }

    [JsonPropertyName("createTimestamp")]
    public string CreateTimestamp { get; set; }

    [JsonPropertyName("gameForumId")]
    public int GameForumId { get; set; }

    [JsonPropertyName("gameForumVo")]
    public GameForumVo GameForumVo { get; set; }

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("gameName")]
    public string GameName { get; set; }

    [JsonPropertyName("headCodeUrl")]
    public string HeadCodeUrl { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("identifyClassify")]
    public int IdentifyClassify { get; set; }

    [JsonPropertyName("identifyNames")]
    public string IdentifyNames { get; set; }

    [JsonPropertyName("ipRegion")]
    public string IpRegion { get; set; }

    [JsonPropertyName("isCopyright")]
    public bool IsCopyright { get; set; }

    [JsonPropertyName("isDown")]
    public bool IsDown { get; set; }

    [JsonPropertyName("isElite")]
    public int IsElite { get; set; }

    [JsonPropertyName("isHide")]
    public bool IsHide { get; set; }

    [JsonPropertyName("isLock")]
    public int IsLock { get; set; }

    [JsonPropertyName("isMine")]
    public int IsMine { get; set; }

    [JsonPropertyName("isOfficial")]
    public int IsOfficial { get; set; }

    [JsonPropertyName("isRecommend")]
    public int IsRecommend { get; set; }

    [JsonPropertyName("isTop")]
    public int IsTop { get; set; }

    [JsonPropertyName("isTransCode")]
    public bool IsTransCode { get; set; }

    [JsonPropertyName("lastEditIpRegion")]
    public string LastEditIpRegion { get; set; }

    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }

    [JsonPropertyName("newIdentifyNames")]
    public List<string> NewIdentifyNames { get; set; }

    [JsonPropertyName("playCount")]
    public int PlayCount { get; set; }

    [JsonPropertyName("postContent")]
    public List<PostContent> PostContent { get; set; }

    [JsonPropertyName("postH5Content")]
    public string PostH5Content { get; set; }

    [JsonPropertyName("postNewH5Content")]
    public string PostNewH5Content { get; set; }

    [JsonPropertyName("postStatus")]
    public int PostStatus { get; set; }

    [JsonPropertyName("postTime")]
    public string PostTime { get; set; }

    [JsonPropertyName("postTitle")]
    public string PostTitle { get; set; }

    [JsonPropertyName("postType")]
    public int PostType { get; set; }

    [JsonPropertyName("postUserId")]
    public string PostUserId { get; set; }

    [JsonPropertyName("publishType")]
    public int PublishType { get; set; }

    [JsonPropertyName("reviewStatus")]
    public int ReviewStatus { get; set; }

    [JsonPropertyName("showRange")]
    public int ShowRange { get; set; }

    [JsonPropertyName("topicList")]
    public List<TopicList> TopicList { get; set; }

    [JsonPropertyName("userHeadCode")]
    public string UserHeadCode { get; set; }

    [JsonPropertyName("userLevel")]
    public int UserLevel { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; }
}
