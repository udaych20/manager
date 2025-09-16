using ServiceManager;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Text.Json.Serialization;
using systems_manager.src.communication;
using systems_manager.src.service;

namespace systems_manager.src.schdulers
{
    public class SystemMetricsScheduler
    {
        private const string CONST_LOG_SYSTEM_METRICS = "System Metrics - ";
        private readonly ILogger<SystemMetricsScheduler> _logger;
        private Timer _timer;
        private readonly ConfigPropertiesReader _configPropertiesReader;
        private readonly DeviceMetricsCloudCommunicationManager _cloudCommunicationManager;
        private readonly DatabaseService _databaseService;
        private readonly string clientId;
        private readonly string topic;
        private readonly string localTestFlag;
        private readonly K3sMetricsService _k3SMetricsService;
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);

        public SystemMetricsScheduler(ConfigPropertiesReader _configPropertiesReader,
            DeviceMetricsCloudCommunicationManager _cloudCommunicationManager,
            K3sMetricsService _k3SMetricsService,
            ILoggerFactory _loggerFactory,
            DatabaseService _databaseService
            )
        {
            _logger = _loggerFactory.CreateLogger<SystemMetricsScheduler>();
            this._databaseService = _databaseService;
            this._configPropertiesReader = _configPropertiesReader;
            // cloud_configurations/device_metrics_client_id -> SM_DEVICE_METRICS_PUBLISH_CLIENT_ID
            clientId = _configPropertiesReader.GetPropertyValue("SM_DEVICE_METRICS_PUBLISH_CLIENT_ID");
            // cloud_configurations/device_metrics_topic -> SM_DEVICE_METRICS_PUBLISH_TOPIC
            topic = _configPropertiesReader.GetPropertyValue("SM_DEVICE_METRICS_PUBLISH_TOPIC");
            // application/enable_local_testing -> SM_APP_ENABLE_LOCAL_TESTING
            localTestFlag = _configPropertiesReader.GetPropertyValue("SM_APP_ENABLE_LOCAL_TESTING");
            this._k3SMetricsService = _k3SMetricsService;
            this._cloudCommunicationManager = _cloudCommunicationManager;
        }

        public void StartScheduler()
        {
            // system_metrics/scheduler_frequency -> SM_SYSTEM_METRICS_SCHEDULER_FREQUENCY
            string frequencyProperty = _configPropertiesReader.GetPropertyValue("SM_SYSTEM_METRICS_SCHEDULER_FREQUENCY");
            _logger.LogInformation("SystemMetricsScheduler.StartScheduler()  frequencyProperty - {frequencyProperty} secs", frequencyProperty);
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
            DeviceMetricsData metrics = null;
            List<DeviceMetricsData> allNodeMetrics = null;
            string clientIdString = $"{clientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
            try
            {
                allNodeMetrics = _k3SMetricsService.GetAllNodeMetrics();
                _logger.LogInformation("{CONST_LOG_SYSTEM_METRICS} - allNodeMetrics - {allNodeMetrics}",CONST_LOG_SYSTEM_METRICS, allNodeMetrics);
                if (allNodeMetrics != null)
                {
                    metrics = allNodeMetrics[0];
                    _logger.LogInformation("allNodeMetrics[0] metrics - {metrics}", metrics);

                }
                foreach (var metric in allNodeMetrics)
                {
                    UpdateNodeMetrics(metric);
                    if ("device".Equals(metric.nodeType))
                    {
                        metrics = metric;
                        _logger.LogInformation("allNodeMetrics[0] device metrics block - {metrics}", metrics);
                    }
                }
                DeviceConnectivityStatus deviceStatus = _databaseService.GetDeviceConnectivityStatusById(1);
                _logger.LogInformation("{CONST_LOG_SYSTEM_METRICS}{metrics} - deviceStatus - {deviceStatus}", CONST_LOG_SYSTEM_METRICS, metrics.deviceId, deviceStatus.status);

                if (deviceStatus.status != "connected" && metrics != null)
                {
                    _databaseService.InsertDeviceMetrics(metrics);
                    var logException = new LogInformation
                    {
                        Topic = topic,
                        InternetConnected = deviceStatus.status,
                        ClientId = clientId,
                        podName = deviceStatus.RunningPodName,
                        Timestamp = DateTime.UtcNow
                    };
                    _databaseService.InsertLog(logException).GetAwaiter();
                    return;
                }

                JsonSerializerOptions jsonSerializerOptions = new()
                {
                    WriteIndented = true,
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
                };
                string json = JsonSerializer.Serialize(metrics, options: jsonSerializerOptions);
                _cloudCommunicationManager.PublishMessage(topic, clientIdString, json);
                DeviceConnectivityStatus deviceConnectivityStatus = _databaseService.GetDeviceConnectivityStatusById(1);
                var logInfo = new LogInformation
                {
                    Topic = topic,
                    InternetConnected = deviceConnectivityStatus.status,
                    ClientId = clientIdString,
                    podName = deviceConnectivityStatus.RunningPodName,
                    Message = $"Message Published: {json}",
                    Timestamp = DateTime.UtcNow
                };
                _databaseService.InsertLog(logInfo).GetAwaiter();
            }
            catch (Exception ex)
            {
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
                _databaseService.InsertLog(logException).GetAwaiter();

                if (localTestFlag.Equals("Y"))
                {
                    metrics = CreateObjectForLocalTesting();
                }
                if (metrics != null)
                {
                    _databaseService.InsertDeviceMetrics(metrics);
                }
            }
        }

        private void UpdateNodeMetrics(DeviceMetricsData metrics)
        {
            UpdateOrInsertNodeMetrics(metrics, "cpuUsage", metrics.cpuUsage);
            UpdateOrInsertNodeMetrics(metrics, "memoryUsage", metrics.memoryUsage); // Assuming metrics.memoryUsage exists
            UpdateOrInsertNodeMetrics(metrics, "memoryUsagePct", metrics.memoryUsagePct);
        }

        private void UpdateOrInsertNodeMetrics(DeviceMetricsData metrics, string resourceName, double resourceValue)
        {
            NodeMetrics nodeMetrics = _databaseService.GetNodeMetricsByNodeType(metrics.nodeType, resourceName);
            _logger.LogInformation("system metrics: {nodeMetrics}", nodeMetrics);
            if (nodeMetrics != null)
            {
                nodeMetrics.ResourceValue = resourceValue;
                nodeMetrics.Timestamp = DateTime.UtcNow;
                nodeMetrics.NodeName = metrics.nodeName;
                nodeMetrics.NodeType = metrics.nodeType;
                nodeMetrics.ResourceName = resourceName;
                _databaseService.UpdateNodeMetrics(nodeMetrics);
            }
            else
            {
                NodeMetrics newNodeMetrics = new NodeMetrics
                {
                    ResourceValue = resourceValue,
                    Timestamp = DateTime.UtcNow,
                    NodeName = metrics.nodeName,
                    NodeType = metrics.nodeType,
                    ResourceName = resourceName
                };
                _databaseService.InsertNodeMetrics(newNodeMetrics);
            }
        }


        private static DeviceMetricsData CreateObjectForLocalTesting()
        {
            return new DeviceMetricsData
            {
                deviceId = 25,
                cpuUsage = 55.5f,
                memoryUsage = 2048.5f,
                totalMemory = 4096.0f,
                storageUsage = 500.0f,
                totalStorage = 1024.0f,
                inputIO = 1000.0f,
                outputIO = 500.0f,
                deleteSwitch = "N",
                createdTime = DateTime.Now,
                lastRebootTime = DateTime.Now,
            };
        }

        public void StopScheduler()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}