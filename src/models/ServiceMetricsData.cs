using SQLite;
using System.ComponentModel.DataAnnotations;

public class ServiceMetricsData
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public long deviceId { get; set; }
    public string serviceName { get; set; }
    public DateTime timestamp { get; set; }
    public string cpuUsage { get; set; }
    public string memoryUsage { get; set; }
    public int podCount { get; set; }
    public int restartCount { get; set; }
    public double errorRate { get; set; } = 0;
    public double responseRate { get; set; } = 0;
    public double inputIO { get; set; } = 0;
    public double outputIO { get; set; } = 0;
    public string deleteSwitch { get; set; }
    public string nodeName { get; set; }
    public string nodeType { get; set; }
}