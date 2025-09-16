using System.Text.Json.Serialization;

public class ServiceUsageDto
    {
        public int sessionId { get; set; }
        public int deviceId { get; set; }
        public string serviceName { get; set; }
        public string usageType { get; set; }
        public string action { get; set; }

        [JsonPropertyName("timestamp")] // For consistent casing with your example
        public DateTime timestamp { get; set; }
        public string status { get; set; }
}