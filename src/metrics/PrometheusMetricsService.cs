using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Policy;

public class PrometheusMetricsService
{
    private readonly HttpClient _httpClient;
    private string _prometheusBaseUrl;
    private ConfigPropertiesReader _configPropertiesReader;

    private readonly ILogger<PrometheusMetricsService> _logger;
    private ILoggerFactory _loggerFactory;

    public PrometheusMetricsService(ConfigPropertiesReader _configPropertiesReader
    , ILoggerFactory _loggerFactory)
    {
        _logger = _loggerFactory.CreateLogger<PrometheusMetricsService>();
        this._loggerFactory = _loggerFactory;
        _httpClient = new HttpClient();
        this._configPropertiesReader = _configPropertiesReader;
        // cloud_configurations/prometheus_metrics_url -> PROMETHEUS_METRICS_URL
        //this._prometheusBaseUrl = _configPropertiesReader.GetPropertyValue("PROMETHEUS_METRICS_URL");
    }

    public async Task<MetricResult> QueryMetricsAsync(string query)
    {
        _logger.LogDebug($"PrometheusMetricsService.QueryMetricsAsync() - in method");

        try
        {
            string url = "http://localhost:8080/api/v1/query?query={query}";
            _logger.LogDebug($"PrometheusMetricsService.QueryMetricsAsync() - {url}");
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            // Assuming the response is in JSON format
            dynamic result = JsonConvert.DeserializeObject(responseBody);
            JToken latestEntry = result["data"]["result"].Last;
            double timestamp = latestEntry["value"][0].Value<double>();
            string value = latestEntry["value"][1].Value<string>();
            _logger.LogInformation($"Query result timestamp: {timestamp} metric value : {value}");
            return new MetricResult(timestamp, value.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, "\nException Caught!");
        }
        _logger.LogInformation("PrometheusMetricsService.QueryMetricsAsync() - sending random value as promotheus not responded");

        Random random = new();
        int randomValue = random.Next(1025, 2060);
        double timestamp1 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new MetricResult(timestamp1, randomValue.ToString());
    }
}