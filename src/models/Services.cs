using SQLite;

public class Service
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string ServiceName { get; set; }
    public string Category { get; set; } // "Functional" or "latency" or "both" or "none"
    public int Priority { get; set; } // 1 to 99
    public string Status { get; set; } // "READY_TO_SCALEOUT", "scaleout", or "reset"
    public string Mode { get; set; } // , "Device", or "Cloud"
}
