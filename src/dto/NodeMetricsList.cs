using System.Collections.Generic;
public class NodeMetricsList
{
    public string Kind { get; set; }
    public string ApiVersion { get; set; }
    public NMetadata Metadata { get; set; }
    public List<NItem> Items { get; set; }
}

public class NMetadata
{
    public string Name { get; set; }
    public string CreationTimestamp { get; set; }
    public Dictionary<string, string> Labels { get; set; }
}

public class NItem
{
    public NMetadata Metadata { get; set; }
    public string Timestamp { get; set; }
    public string Window { get; set; }
    public NUsage Usage { get; set; }
}

public class NUsage
{
    public string Cpu { get; set; }
    public string Memory { get; set; }
}