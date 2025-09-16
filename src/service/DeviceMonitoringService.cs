using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class DeviceMonitoringService
{
    private int CpuThreshold { get; set; }
    private int MemoryThreshold { get; set; }
    private int DeviceId { get; set; }
    private readonly ILogger<DeviceMonitoringService> _logger;
    private string CloudSystemsManagerURL { get; set; }

    // Explicitly declare the type of the handler field
    private static readonly HttpClientHandler handler = new HttpClientHandler
    {
        // Bypass SSL certificate validation (for development purposes only)
        ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
    };

    private static readonly HttpClient client = new HttpClient(handler);

    public DeviceMonitoringService(ConfigPropertiesReader configProperties, ILoggerFactory loggerFactory)
    {
        CpuThreshold = int.Parse(configProperties.GetPropertyValue("SM_CPU_SCALEOUT_THRESHOLD"));
        MemoryThreshold = int.Parse(configProperties.GetPropertyValue("SM_APP_MEMORY_SCALEOUT_THRESHOLD"));
        string cloudCareIP = configProperties.GetPropertyValue("CLOUD_CARE_HOST");
        string assetManagerPort = configProperties.GetPropertyValue("ASSET_MANAGER_PORT");
        CloudSystemsManagerURL = $"https://{cloudCareIP}/{configProperties.GetPropertyValue("SM_UPDATE_SERVICE_STATUS_URI")}";
        DeviceId = int.Parse(configProperties.GetPropertyValue("DEVICE_ID"));
        _logger = loggerFactory.CreateLogger<DeviceMonitoringService>();
    }

    public bool CheckForHighUsage(float cpuUsage, float memUsage)
    {
        _logger.LogInformation("Checking for high resource usage: CpuThreshold - {CpuThreshold}, Usage - {CpuUsage}, MemoryThreshold - {MemoryThreshold}, MemUsage - {MemUsage}", CpuThreshold, cpuUsage, MemoryThreshold, memUsage);
        return cpuUsage > CpuThreshold && memUsage > MemoryThreshold;
    }

    public async Task ActivatePolicyAsync(string serviceName, string status)
    {
        var payload = new
        {
            deviceId = DeviceId,
            service = serviceName,
            status = status
        };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            string token = "db8e1d0a-a5c7-4457-b943-9b4f4160a0b1";
            client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage response = await client.PostAsync(CloudSystemsManagerURL, content);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Request: {jsonPayload}, Response: {responseBody}", jsonPayload, responseBody);
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Exception while activating policy");
            throw;
        }
    }
}