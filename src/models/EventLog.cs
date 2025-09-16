using SQLite;
using System;

[Table("EventLog")]
public class EventLog
{
    [PrimaryKey, AutoIncrement]
    public int LogId { get; set; }
    [MaxLength(255)]
    public string PolicyName { get; set; }
    [MaxLength(255)]
    public string ServiceName { get; set; }
    public string ActionTaken { get; set; }
    public string DestinationNodeType { get; set; }
    public Boolean IsNotification { get; set; }
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } // JSON format

    public override string ToString()
    {
        return $"LogId: {LogId}, PolicyName: {PolicyName}, ServiceName: {ServiceName}, " +
               $"ActionTaken: {ActionTaken}, IsNotification: {IsNotification}, " +
               $"DestinationNodeType: {DestinationNodeType}, Timestamp: {Timestamp}, Details: {Details}";
    }
}
