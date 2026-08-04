using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Api.Models.KuroClient.Options
{
    public class HomeFeedSharedOption
    {
        public string GameId { get; set; }

        public string PostId { get; set; }

        public virtual Dictionary<string, string> ConvertParam()
        {
            return new Dictionary<string, string> { { "gameId", GameId }, { "postId", PostId } };
        }

        public static HomeFeedSharedOption CreateWaves(string postId)
        {
            return new() { GameId = "2", PostId = postId };
        }

        public static HomeFeedSharedOption CreatePunish(string postId)
        {
            return new() { GameId = "3", PostId = postId };
        }
    }
}
