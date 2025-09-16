using SQLite;

public class DeviceMonitoringStatus
{
    [PrimaryKey,AutoIncrement]
    public int Id { get; set; }
    public bool isDeviceResourceUsageHigh { get; set; }  
    public DateTime timestamp { get; set; }
}