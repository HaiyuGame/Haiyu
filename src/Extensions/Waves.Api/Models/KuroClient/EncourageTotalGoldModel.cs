using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient
{
    public class EncourageTotalGoldModel
    {
        [JsonPropertyName("goldNum")]
        public double GoldNum { get; set; }
    }
}
