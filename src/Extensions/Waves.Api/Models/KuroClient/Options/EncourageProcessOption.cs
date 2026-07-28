using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using Waves.Api.Models.Enums;

namespace Waves.Api.Models.KuroClient.Options
{
    public class EncourageProcessOption
    {
        public string GameId { get; set; }

        public string UserId { get; set; }

        public Dictionary<string, string> ConvertParam()
        {
            return new Dictionary<string, string> { { "gameId", GameId }, { "userId", UserId } };
        }

        public static EncourageProcessOption CreateDefault(string userId)
        {
            return new() { GameId = "0", UserId = userId };
        }
    }
}
