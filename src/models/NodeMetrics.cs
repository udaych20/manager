using SQLite;
using System;


[Table("Node_Metrics")]
public class NodeMetrics
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; } // Assuming there's an ID field for uniqueness

    [MaxLength(50)]
    public string NodeName { get; set; }
    
    [MaxLength(10)]
    public string NodeType { get; set; }

    [MaxLength(50)]
    public string ResourceName { get; set; } // Assuming values like 'cpu' or 'memory'

    public double ResourceValue { get; set; }

    public DateTime Timestamp { get; set; }
}
