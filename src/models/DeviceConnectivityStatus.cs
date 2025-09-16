using SQLite;

public class DeviceConnectivityStatus
{
    [PrimaryKey]
    public int Id { get; set; }
    public string status { get; set; }
    public string RunningPodName { get; set; }
    public DateTime timestamp { get; set; }
}