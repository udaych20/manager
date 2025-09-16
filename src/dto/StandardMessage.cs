using System;
public class StandardMessage
{
    public string MessageType { get; set; }
    public string Message { get; set; }
    public long Timestamp { get; set; }
    public string Source { get; set; }
    public int Priority { get; set; }
    public int RetryCount { get; set; }
    public long Expiry { get; set; }
    public override string ToString()
    {
        return $"MessageType: {MessageType}, Message: {Message}, Timestamp: {Timestamp}, Source: {Source}, Priority: {Priority}, RetryCount: {RetryCount}, Expiry: {Expiry}";
    }
}