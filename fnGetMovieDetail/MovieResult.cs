using System;
using Newtonsoft.Json;

namespace fnGetMovieDetail
{
    public class MovieResult
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonProperty("title")]
        public string? Title { get; set; }
        
        [JsonProperty("year")]
        public string? Year { get; set; }
        
        [JsonProperty("thumb")]
        public string? Thumb { get; set; }

        [JsonProperty("video")]
        public string? video { get; set; }

    }
}
