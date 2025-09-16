using Newtonsoft.Json;
public class SensorHeartBeatData
{
    [JsonProperty("current_timestamp")]
    public string CurrentTimestamp { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; }
}