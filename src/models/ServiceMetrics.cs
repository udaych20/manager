using SQLite;
using System;

[Table("Service_Metrics")]
public class ServiceMetrics
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; } // Assuming there's an ID field for uniqueness

    [MaxLength(50)]
    public string NodeName { get; set; }
    [MaxLength(10)]
    public string NodeType { get; set; } 

    [MaxLength(100)]
    public string ServiceName { get; set; }

    [MaxLength(50)]
    public string ResourceName { get; set; } // Assuming values like 'cpu', 'memory', etc.

    public double ResourceValue { get; set; }

    public DateTime Timestamp { get; set; }
}
