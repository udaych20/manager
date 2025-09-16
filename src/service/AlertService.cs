using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class AlertService
{
    private readonly ConfigPropertiesReader _configPropertiesReader;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlertService> _logger;

    public AlertService(ConfigPropertiesReader configPropertiesReader, ILoggerFactory loggerFactory)
    {
        _configPropertiesReader = configPropertiesReader ?? throw new ArgumentNullException(nameof(configPropertiesReader));
        
        var handler = new HttpClientHandler
        {
            // Bypass SSL certificate validation (for development purposes only)
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        
        _httpClient = new HttpClient(handler);
        string token = "db8e1d0a-a5c7-4457-b943-9b4f4160a0b1";
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        _logger = loggerFactory.CreateLogger<AlertService>();
    }

    public async Task<bool> RaiseAlertAsync(string value, string description, string destinationName,
            string destinationType, string errorDetails, string raisedBy)
    {
        _logger.LogInformation("RaiseAlertAsync called");
        
        var alert = new
        {
            createdTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
            value = _configPropertiesReader.GetPropertyValue("DEVICE_ID"),
            description,
            destinationId = _configPropertiesReader.GetPropertyValue("DEVICE_ID"),
            destinationName,
            destinationType,
            deviceId = _configPropertiesReader.GetPropertyValue("DEVICE_ID"),
            deviceName = _configPropertiesReader.GetPropertyValue("DEVICE_ID"),
            errorDetails,
            eventCode = _configPropertiesReader.GetPropertyValue("SM_APP_SCALEOUT_EVENTCODE"),
            raisedBy,
            category = "Critical"
        };

        var json = JsonConvert.SerializeObject(alert);
        _logger.LogInformation($"Alert JSON: {json}");
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            string cloudCareIP = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
            string alertApiURI = _configPropertiesReader.GetPropertyValue("ALERT_API_URI");
            var response = await _httpClient.PostAsync($"https://{cloudCareIP}/{alertApiURI}", content);
            
            _logger.LogInformation("Alert raised for: {content}, Response: {response}", json, response);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while raising the alert.");
            return false;
        }
    }
}