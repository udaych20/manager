using ServiceManager;
using System.Text.Json;
using System.Text.Json.Serialization;
using systems_manager.src.communication;
using systems_manager.src.service;

namespace systems_manager.src.schdulers
{
    public class ServiceMetricsScheduler
    {
        private Timer _timer;
        private readonly ConfigPropertiesReader _configPropertiesReader;
        private readonly K3sMetricsService _k3SMetricsService;
        private readonly ServiceMetricsCloudCommunicationManager _cloudCommunicationManager;
        private readonly string _namespaceString = string.Empty;
        private readonly DatabaseService _databaseService;
        private readonly string clientId;
        private readonly string topic;
        private readonly string localTestFlag;
        private readonly ILogger<ServiceMetricsScheduler> _logger;

        public ServiceMetricsScheduler(ConfigPropertiesReader configPropertiesReader,
            ServiceMetricsCloudCommunicationManager _cloudCommunicationManager,
            K3sMetricsService _k3SMetricsService,
            ILoggerFactory _loggerFactory,
            DatabaseService databaseService)
        {
            _logger = _loggerFactory.CreateLogger<ServiceMetricsScheduler>();
            try
            {
                _databaseService = databaseService;
                _configPropertiesReader = configPropertiesReader;
                _namespaceString = _configPropertiesReader.GetPropertyValue("SM_NAMESPACE_FOR_READING_METRICS");
                //clientId = _configPropertiesReader.GetPropertyValue("SM_SERVICE_METRICS_PUBLISH_CLIENT");
                string deviceId = _configPropertiesReader.GetPropertyValue("DEVICE_ID");
                clientId = "SM_SER_MET_CLIENT_" + deviceId ;
                this.topic = _configPropertiesReader.GetPropertyValue("SM_SERVICE_METRICS_PUBLISH_TOPIC");
                localTestFlag = _configPropertiesReader.GetPropertyValue("SM_APP_ENABLE_LOCAL_TESTING");
                this._k3SMetricsService = _k3SMetricsService;
                this._cloudCommunicationManager = _cloudCommunicationManager;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception while object construction");
            }
        }

        public void StartScheduler()
        {
            string frequencyProperty = _configPropertiesReader.GetPropertyValue("SM_SERVICE_METRICS_SCHEDULER_FREQUENCY");
            _logger.LogDebug("ServiceMetricsScheduler.StartScheduler()  frequencyProperty - {frequencyProperty}", frequencyProperty);
            if (int.TryParse(frequencyProperty, out int frequencySeconds))
            {
                _timer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(frequencySeconds));
            }
            else
            {
                throw new ArgumentException("Invalid frequency value in the properties file");
            }
        }

        private void CollectMetrics(object state)
        {
            List<ServiceMetricsData> metrics;
            try
            {
                metrics = _k3SMetricsService.getPodMetrics(_namespaceString, "pods");
                _logger.LogDebug("service metrics - {metrics}", metrics?.Count ?? 0);
                if (metrics == null || !metrics.Any())
                {
                    _logger.LogDebug("service metrics returning null or empty");
                    return;
                }

                UpdateServiceMetrics(metrics);
                DeviceConnectivityStatus deviceStatus = _databaseService.GetDeviceConnectivityStatusById(1);
                _logger.LogDebug("service deviceStatus - {deviceStatus}", deviceStatus.status);
                if (deviceStatus.status != "connected")
                {
                    foreach (ServiceMetricsData serviceMetrics in metrics)
                    {
                        _databaseService.InsertServiceMetrics(serviceMetrics);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                if (localTestFlag.Equals("Y"))
                {
                    metrics = CreateObjectForLocalTesting();
                }
                _logger.LogError(e, "Error While collecting metrics");
                return;
            }
            string clientIdString = $"{clientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
            try
            {
                JsonSerializerOptions jsonSerializerOptions = new()
                {
                    WriteIndented = true,
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
                };
                string json = JsonSerializer.Serialize(metrics, options: jsonSerializerOptions);
                _cloudCommunicationManager.PublishMessage(this.topic, clientIdString, json);
                DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                var logInfo = new LogInformation
                {
                    Topic = topic,
                    ClientId = clientIdString,
                    InternetConnected = status.status,
                    podName = status.RunningPodName,
                    Message = $"Message Published: {json}",
                    Timestamp = DateTime.UtcNow
                };
                _databaseService.InsertLog(logInfo).GetAwaiter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Collect Metrics Exception Details.");
                DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                var logException = new LogInformation
                {
                    Topic = topic,
                    InternetConnected = status.status,
                    ClientId = clientIdString,
                    podName = status.RunningPodName,
                    Exception = $"Exception: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
                // Note: Consider handling the exception more appropriately here, e.g., logging or retry logic.
            }
        }

        private void UpdateServiceMetrics(List<ServiceMetricsData> serviceMetrics)
        {
            foreach (var metrics in serviceMetrics)
            {
                UpdateOrInsertNodeMetrics(metrics, "cpuUsage", double.Parse(metrics.cpuUsage));
                UpdateOrInsertNodeMetrics(metrics, "memoryUsage", double.Parse(metrics.memoryUsage));
                UpdateOrInsertNodeMetrics(metrics, "inputIO", metrics.inputIO);
                UpdateOrInsertNodeMetrics(metrics, "outputIO", metrics.outputIO);
            }
        }

        private void UpdateOrInsertNodeMetrics(ServiceMetricsData metrics, string resourceName, double resourceValue)
        {
            _logger.LogDebug("service metrics - node name {metrics.nodeName}", metrics.nodeName);
            ServiceMetrics serviceMetrics = _databaseService.GetNodeWiseServiceMetricsBynodeTypeAndResourceName(metrics.nodeName, resourceName);
            if (serviceMetrics != null)
            {
                serviceMetrics.ResourceValue = resourceValue;
                serviceMetrics.Timestamp = DateTime.UtcNow;
                serviceMetrics.NodeName = metrics.nodeName;
                serviceMetrics.NodeType = metrics.nodeType;
                serviceMetrics.ServiceName = metrics.serviceName;
                _databaseService.UpdateServiceMetrics(serviceMetrics);
            }
            else
            {
                ServiceMetrics serviceMetricsNew = new ServiceMetrics
                {
                    ResourceValue = resourceValue,
                    Timestamp = DateTime.UtcNow,
                    NodeName = metrics.nodeName,
                    NodeType = metrics.nodeType,
                    ServiceName = metrics.serviceName,
                    ResourceName = resourceName // Assuming NodeMetrics class has a ResourceName property
                };
                _databaseService.InsertServiceMetrics(serviceMetricsNew);
            }
        }

        private List<ServiceMetricsData> CreateObjectForLocalTesting()
        {
            List<ServiceMetricsData> metrics;
            ServiceMetricsData testMetricsData = new ServiceMetricsData
            {
                deviceId = 25, // Example deviceId
                serviceName = "sensor-reader",
                timestamp = DateTime.Now, // Current timestamp
                cpuUsage = "25", // Example CPU usage
                memoryUsage = "512", // Example memory usage
                podCount = 3, // Example pod count
                restartCount = 1, // Example restart count
                errorRate = 0.01, // Example error rate
                responseRate = 99.9, // Example response rate
                inputIO = 1000.5, // Example input IO
                outputIO = 500.5, // Example output IO
                deleteSwitch = "N"
            };
            metrics = new List<ServiceMetricsData> { testMetricsData };
            foreach (ServiceMetricsData serviceMetrics in metrics)
            {
                _databaseService.InsertServiceMetrics(serviceMetrics);
            }

            return metrics;
        }

        public void StopScheduler()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }
    }
}