using Newtonsoft.Json;

public class K3sServiceMetricDto
{
    public string ApiVersion { get; set; }
    public List<K3sItem> Items { get; set; }
    public string Kind { get; set; }
    public Metadata Metadata { get; set; }
}

public class K3sItem
{
    public string ApiVersion { get; set; }
    public string Kind { get; set; }
    public K3sMetadata Metadata { get; set; }
    public Spec Spec { get; set; }
    public Status Status { get; set; }
}

public class K3sMetadata
{
    public string Name { get; set; }

    [JsonProperty("namespace")]
    public string NamespaceProperty { get; set; }
    public string CreationTimestamp { get; set; }
    public string ResourceVersion { get; set; }
    public Dictionary<string, string> Labels { get; set; }
    // Other properties as needed
}

public class Spec
{
    public List<K3sContainer> Containers { get; set; }
    public string RestartPolicy { get; set; }
    public List<Volume> Volumes { get; set; }
    // Other properties as needed
}

public class K3sContainer
{
    public string Name { get; set; }
    public string Image { get; set; }
    public int ContainerPort { get; set; }
    // Other properties as needed
}

public class Volume
{
    public string Name { get; set; }
    // Other properties as needed
}

public class Status
{
    public string Phase { get; set; }
    public List<ContainerStatus> ContainerStatuses { get; set; }
    // Other properties as needed
}

public class ContainerStatus
{
    public string Name { get; set; }
    public bool Ready { get; set; }
    public int RestartCount { get; set; }
    public State State { get; set; }
}

public class State
{
    public Running Running { get; set; }
    // Other states as needed
}

public class Running
{
    public string StartedAt { get; set; }
}