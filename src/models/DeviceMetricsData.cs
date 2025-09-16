using SQLite;
public class DeviceMetricsData
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    [Indexed]
    public long deviceId { get; set; }
    public float cpuUsage { get; set; }
    public float memoryUsage { get; set; }
    public float memoryUsagePct { get; set; }
    public float totalMemory { get; set; }
    public float storageUsage { get; set; }
    public float totalStorage { get; set; }
    public float inputIO { get; set; }
    public float outputIO { get; set; }
    public string deleteSwitch { get; set; }
    public DateTime createdTime { get; set; }
    public DateTime lastRebootTime { get; set; }
    public string nodeName { get; set; }
    public string nodeType { get; set; }
}
