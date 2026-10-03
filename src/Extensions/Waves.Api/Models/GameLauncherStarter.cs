using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Waves.Api.Models
{
    [JsonSerializable(typeof(Activity))]
    [JsonSerializable(typeof(Content))]
    [JsonSerializable(typeof(Guidance))]
    [JsonSerializable(typeof(News))]
    [JsonSerializable(typeof(Notice))]
    [JsonSerializable(typeof(GameLauncherStarter))]
    [JsonSerializable(typeof(Slideshow))]
    public partial class GameLauncherStarterContext : JsonSerializerContext { }

    public class Activity
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }

        [JsonPropertyName("sort")]
        public int Sort { get; set; }

        [JsonPropertyName("functionSwitch")]
        public int FunctionSwitch { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("content")]
        public string TContent { get; set; }

        [JsonPropertyName("jumpUrl")]
        public string JumpUrl { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; }
    }

    public class Guidance
    {
        [JsonPropertyName("activity")]
        public Activity Activity { get; set; }

        [JsonPropertyName("desc")]
        public string Desc { get; set; }

        [JsonPropertyName("news")]
        public News News { get; set; }

        [JsonPropertyName("notice")]
        public Notice Notice { get; set; }
    }

    public class News
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class Notice
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }

    public class GameLauncherStarter
    {
        [JsonPropertyName("guidance")]
        public Guidance Guidance { get; set; }

        [JsonPropertyName("slideshow")]
        public List<Slideshow> Slideshow { get; set; }
    }

    public class Slideshow
    {
        [JsonPropertyName("carouselNotes")]
        public string CarouselNotes { get; set; }

        [JsonPropertyName("jumpUrl")]
        public string JumpUrl { get; set; }

        [JsonPropertyName("md5")]
        public string Md5 { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
