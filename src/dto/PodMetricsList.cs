using Newtonsoft.Json;

public class PodMetricsList
{
    [JsonProperty("items")]
    public List<Item> Items { get; set; }
}

public class Item
{
    internal object ContainerStatus;

    [JsonProperty("metadata")]
    public Metadata Metadata { get; set; }

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("containers")]
    public List<Container> Containers { get; set; }

    [JsonProperty("Items")]
    public List<Item> item { get; set; }

}

public class Metadata
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("creationTimestamp")]
    public DateTime CreationTimestamp { get; set; }

    [JsonProperty("labels")]
    public Labels labels { get; set; }
}

public class Labels
{

    [JsonProperty("nodeType")]
    public string nodeType { get; set; }

    [JsonProperty("service")]
    public string service { get; set; }

    [JsonProperty("serviceType")]
    public string serviceType { get; set; }

    [JsonProperty("workloadType")]
    public string workloadType { get; set; }

    [JsonProperty("usageType")]
    public string usageType { get; set; }

}

public class Container
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("usage")]
    public Usage Usage { get; set; }
}

public class Usage
{
    [JsonProperty("cpu")]
    public string Cpu { get; set; }

    [JsonProperty("memory")]
    public string Memory { get; set; }
}
