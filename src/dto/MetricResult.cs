public class MetricResult
{
    public double Timestamp { get; set; }
    public string Value { get; set; }

    public MetricResult(double timestamp, string value)
    {
        Timestamp = timestamp;
        Value = value;
    }
}
