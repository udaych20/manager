using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class CloudService
{
    private readonly ConfigPropertiesReader _configPropertiesReader;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudService> _logger;
    private readonly string CloudSystemsManagerURL;
    private readonly long deviceId;

    public CloudService(ConfigPropertiesReader configPropertiesReader, ILoggerFactory loggerFactory)
    {
        _configPropertiesReader = configPropertiesReader ?? throw new ArgumentNullException(nameof(configPropertiesReader));
        _logger = loggerFactory.CreateLogger<CloudService>();

        // Initialize HttpClient with custom handler to bypass SSL validation (for development purposes only)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        _httpClient = new HttpClient(handler);
        string token = "db8e1d0a-a5c7-4457-b943-9b4f4160a0b1";
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Construct the Cloud Systems Manager URL
        string cloudCareIP = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
        CloudSystemsManagerURL = $"https://{cloudCareIP}/{_configPropertiesReader.GetPropertyValue("SM_UPDATE_SERVICE_STATUS_URI")}";

        // Retrieve device ID from configuration
        deviceId = long.Parse(_configPropertiesReader.GetPropertyValue("DEVICE_ID"));
    }

    public async Task UpdateServiceStatusAsync(string serviceName, string status, string destinationNodeType, string actionType)
{
    // Create the payload for the request
    var payload = new
    {
        deviceId,
        service = serviceName,
        status,
        destinationNodeType,
        actionType
    };

    string jsonPayload = JsonConvert.SerializeObject(payload);
    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

    try
    {
        // Log the request details
        _logger.LogInformation("Sending POST request to URL: {URL}", CloudSystemsManagerURL);
        _logger.LogInformation("Request Payload: {Payload}", jsonPayload);

        // Send the POST request
        HttpResponseMessage response = await _httpClient.PostAsync(CloudSystemsManagerURL, content);

        // Log the response status code
        _logger.LogInformation("Response Status Code: {StatusCode}", response.StatusCode);

        // Log the response content if the status code is not successful
        if (!response.IsSuccessStatusCode)
        {
            string errorResponseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Request failed. Response: {Response}", errorResponseBody);
        }
        else
        {
            // Read and log the successful response
            string responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Request successful. Response: {Response}", responseBody);
        }

        response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException e)
    {
        // Log any exceptions that occur
        _logger.LogError(e, "Exception while updating service status for: {ServiceName}", serviceName);
        throw;
    }
    }
}