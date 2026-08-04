using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Waves.Api.Models.KuroClient.Options;

public class HomeFeedLikeOption : HomeFeedOption
{
    public string PostId { get; set; }

    public string PostType { get; set; }

    /// <summary>
    /// 1是点赞，2是取消点赞
    /// </summary>
    public string OperateType { get; set; }
    public string LikeType { get; set; }
    public string PostCommentId { get; set; }
    public string PostCommentReplyId { get; set; }
    public string ToUserId { get; set; }

    public override Dictionary<string, string> ConvertParam()
    {
        return new Dictionary<string, string>
        {
            { "forumId", ForumId },
            { "gameId", GameId },
            { "likeType", LikeType },
            { "postId", PostId },
            { "postType", PostType },
            { "operateType", OperateType },
            { "postCommentId", PostCommentId },
            { "postCommentReplyId", PostCommentReplyId },
            { "toUserId", ToUserId },
        };
    }

    public static HomeFeedLikeOption CreateLikeWaves(
        string postId,
        string postType,
        string operateType,
        string postCommandId,
        string postCommentReplayIdk,
        string toUserId
    )
    {
        return new()
        {
            ForumId = "9",
            GameId = "3",
            PostId = postId,
            LikeType ="1",
            PostType = postType,
            OperateType = operateType,
            PostCommentId = postCommandId,
            PostCommentReplyId = postCommentReplayIdk,
            ToUserId = toUserId,
        };
    }

    public static HomeFeedLikeOption CreateLikePunish(
        string postId,
        string postType,
        string operateType,
        string postCommandId,
        string postCommentReplayIdk,
        string toUserId
    )
    {
        return new()
        {
            ForumId = "2",
            GameId = "2",
            LikeType = "1",
            PostId = postId,
            PostType = postType,
            OperateType = operateType,
            PostCommentId = postCommandId,
            PostCommentReplyId = postCommentReplayIdk,
            ToUserId = toUserId,
        };
    }
}
