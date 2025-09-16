using k8s;
using Newtonsoft.Json;
using System.Text.Json;
using systems_manager.src.util;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using systems_manager.src.service;
using System.Reflection.Emit;

namespace ServiceManager
{
    public class K3sMetricsService
    {
        private readonly ILoggerFactory _loggerFactory;
        public ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger { get; }
        private readonly ILogger<K3sMetricsService> _logger;
        private readonly string localTestFlag;
        private readonly float totalMemoryInGB;
        private readonly Kubernetes _kubernetesClient;
        private readonly HttpClient _httpClient;
        private long deviceId;
        //private DeviceManager deviceManager;
        private int cpuCores;
        private float totalMemory;
        private String nodeName;
        private PrometheusMetricsService prometheusMetricsService;
        private DatabaseService _databaseService;
        private string k3sNamespace;

        public K3sMetricsService(ConfigPropertiesReader _configProperties,
            ILoggerFactory _loggerFactory,
            DatabaseService _databaseService)
        {
            this._loggerFactory = _loggerFactory;
            this._k3sMetricsUtilLogger = _loggerFactory.CreateLogger<K3sMetricsUtil>();
            _logger = _loggerFactory.CreateLogger<K3sMetricsService>();

            // application/enable_local_testing -> SM_APP_ENABLE_LOCAL_TESTING
            localTestFlag = _configProperties.GetPropertyValue("SM_APP_ENABLE_LOCAL_TESTING");
            // application/k3s_namespace -> SM_APP_K3S_NAMESPACE
            this.k3sNamespace = _configProperties.GetPropertyValue("SM_APP_K3S_NAMESPACE");
            // application/Total_Device_Memory_For_ScaleOut_Threshold_Calculation -> SM_TOT_DEVICE_MEM_SCALEOUT_THRESHOLD_CALC
            this.totalMemoryInGB = float.Parse(_configProperties.GetPropertyValue("SM_TOT_DEVICE_MEM_SCALEOUT_THRESHOLD_CALC"));
            KubernetesClientConfiguration config;
            if (!localTestFlag.Equals("Y"))
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            }

            _kubernetesClient = new Kubernetes(config);
            // application/asset_manager_url -> ASSET_MANAGER_URL
            string assetManagerUrl = _configProperties.GetPropertyValue("ASSET_MANAGER_URL");
            //deviceManager = new DeviceManager(assetManagerUrl);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(config.Host)
            };

            // Assuming the token is used for authentication
            if (!string.IsNullOrEmpty(config.AccessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
            }

            // Skip TLS validation - not recommended for production
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            this.nodeName = ReadCpuCoresForNodeAndPodMetrics().GetAwaiter().GetResult();
            // application/deviceId -> DEVICE_ID
            this.deviceId = long.Parse(_configProperties.GetPropertyValue("DEVICE_ID"));
            this.prometheusMetricsService = new PrometheusMetricsService(_configProperties, _loggerFactory);
            this._databaseService = _databaseService;
        }

        public List<ServiceMetricsData> getPodMetrics(string namespaceParameter, string nodesOrPods, string serviceName = "")
        {
            var group = "metrics.k8s.io";
            var version = "v1beta1";
            string labelSelector = $"app={serviceName}";
            try
            {
                var result = _kubernetesClient.ListNamespacedCustomObject(group, version, namespaceParameter, nodesOrPods);
                _logger.LogDebug($"result - {result}");
                return K3sMetricsUtil.ReadMetrics(result.ToString(), this.deviceId, prometheusMetricsService, _databaseService, _k3sMetricsUtilLogger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error While fetching Pod Metrics");
                throw;
            }
        }

        public DeviceMetricsData GetNodeMetrics()
        {
            var group = "metrics.k8s.io"; // The API group for metrics
            var version = "v1beta1"; // The API version
            DeviceMetricsData data = new DeviceMetricsData();
            try
            {
                // Fetch metrics for all nodes in the cluster
                var result = _kubernetesClient.ListClusterCustomObject(group, version, "nodes");
                NodeMetricsList metricsList = JsonConvert.DeserializeObject<NodeMetricsList>(result.ToString());

                data.deviceId = this.deviceId;
                data.createdTime = DateTime.Now;
                data.lastRebootTime = DateTime.Now;
                data.deleteSwitch = "N";
                foreach (var item in metricsList.Items)
                {
                    data.nodeName = item.Metadata.Name;
                    data.nodeType = item.Metadata.Labels.GetValueOrDefault("nodeType");
                    _logger.LogInformation($"Node Metrics Lables: {data.nodeType}");

                    data.cpuUsage = float.Parse(K3sMetricsUtil.CalculateCpuUsageNodePercentage(item.Usage.Cpu, cpuCores, _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    data.memoryUsage = float.Parse(K3sMetricsUtil.ConvertMemoryUsage(item.Usage.Memory, _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    data.memoryUsagePct = K3sMetricsUtil.ConvertMemoryUsagePct(item.Usage.Memory, totalMemory, _k3sMetricsUtilLogger);
                }
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNodeMetrics");
                throw;
            }
        }

        public List<DeviceMetricsData> GetAllNodeMetrics()
        {
            var group = "metrics.k8s.io"; // The API group for metrics
            var version = "v1beta1"; // The API version
            List<DeviceMetricsData> nodesData = new List<DeviceMetricsData>();

            try
            {
                // Fetch metrics for all nodes in the cluster
                var result = _kubernetesClient.ListClusterCustomObject(group, version, "nodes");
                NodeMetricsList metricsList = JsonConvert.DeserializeObject<NodeMetricsList>(result.ToString());

                foreach (var item in metricsList.Items)
                {
                    DeviceMetricsData data = new DeviceMetricsData
                    {
                        deviceId = this.deviceId,
                        createdTime = DateTime.Now,
                        lastRebootTime = DateTime.Now,
                        deleteSwitch = "N",
                        nodeName = item.Metadata.Name,
                        nodeType = item.Metadata.Labels.GetValueOrDefault("nodeType", "device")
                    };

                    _logger.LogInformation($"Node Metrics Labels: {data.nodeType}");

                    data.cpuUsage = float.Parse(K3sMetricsUtil.CalculateCpuUsageNodePercentage(item.Usage.Cpu, cpuCores, _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    data.memoryUsage = float.Parse(K3sMetricsUtil.ConvertMemoryUsage(item.Usage.Memory, _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    data.memoryUsagePct = K3sMetricsUtil.ConvertMemoryUsagePct(item.Usage.Memory, totalMemory, _k3sMetricsUtilLogger);
                    nodesData.Add(data);
                }
                return nodesData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllNodeMetrics");
                throw;
            }
        }

        public DeviceMetricsData GetDeviceMetrics()
        {
            var group = "metrics.k8s.io"; // The API group for metrics
            var version = "v1beta1"; // The API version
            DeviceMetricsData data = new DeviceMetricsData();
            try
            {
                // Fetch metrics for all nodes in the cluster
                var result = _kubernetesClient.ListClusterCustomObject(group, version, "nodes");
                NodeMetricsList metricsList = JsonConvert.DeserializeObject<NodeMetricsList>(result.ToString());
                data.deviceId = this.deviceId;
                data.createdTime = DateTime.Now;
                data.lastRebootTime = DateTime.Now;
                data.deleteSwitch = "N";
                foreach (var item in metricsList.Items)
                {
                    data.nodeName = item.Metadata.Name;
                    data.nodeType = item.Metadata.Labels.GetValueOrDefault("nodeType");
                    data.cpuUsage = float.Parse(K3sMetricsUtil.CalculateCpuUsageNodePercentage(item.Usage.Cpu, cpuCores, _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    data.memoryUsage = K3sMetricsUtil.CalculateMemoryUsagePercentage(K3sMetricsUtil.ConvertMemoryUsageKibIntoGB(item.Usage.Memory, _k3sMetricsUtilLogger), totalMemoryInGB);
                }
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDeviceMetrics");
                throw;
            }
        }

        public async void ListPodsInNamespaceWithLabel(string namespaceName, string serviceName)
        {
            string labelSelector = $"app={serviceName}"; // Constructing label selector

            try
            {
                var pods = await _kubernetesClient.ListNamespacedPodAsync(namespaceName, labelSelector: labelSelector);
                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(pods, Newtonsoft.Json.Formatting.Indented);
                int podsCount = pods.Items.Count;
                int totalRestartCount = 0;
                foreach (var pod in pods.Items)
                {
                    foreach (var containerStatus in pod.Status.ContainerStatuses)
                    {
                        totalRestartCount += containerStatus.RestartCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Details: {Exception}", ex);
            }
        }

        public async Task PrintFormattedPodsJsonAsync()
        {
            try
            {
                var pods = await _kubernetesClient.ListNamespacedPodAsync("default");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // This option ensures the JSON is formatted
                };
                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(pods, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Details:");
            }
        }

        public IEnumerable<string> GetServicesStatus()
        {
            var services = _kubernetesClient.ListNamespacedServiceAsync("default").GetAwaiter().GetResult();
            var serviceStatuses = services.Items.Select(service => $"{service.Metadata.Name}: {service.Status}");

            return serviceStatuses;
        }

        public IEnumerable<string> GetPodsHealth()
        {
            var pods = _kubernetesClient.ListNamespacedPodAsync("default").GetAwaiter().GetResult();
            var podHealthStatuses = pods.Items.Select(pod =>
            {
                var conditions = pod.Status.Conditions.Where(c => c.Type == "Ready" && c.Status == "True");
                var isHealthy = conditions.Any();
                return $"{pod.Metadata.Name}: {(isHealthy ? "Healthy" : "Unhealthy")}";
            });

            return podHealthStatuses;
        }

        public IEnumerable<string> GetPodStartupTimes()
        {
            var pods = _kubernetesClient.ListNamespacedPodAsync("default").GetAwaiter().GetResult();
            var podStartupTimes = pods.Items.Select(pod => $"{pod.Metadata.Name}: {pod.Status.StartTime}");

            return podStartupTimes;
        }

        public IEnumerable<string> GetPodMetrics()
        {
            var metricsUrl = "https://metrics-server/apis/metrics.k8s.io/v1beta1/pods";
            var response = _httpClient.GetAsync(metricsUrl).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var podMetrics = JsonConvert.DeserializeObject<PodMetricsList>(content);

            var metricsInfo = new List<string>();
            foreach (var pod in podMetrics.Items)
            {
                foreach (var container in pod.Containers)
                {
                    var cpuUsage = container.Usage.Cpu;
                    var memoryUsage = container.Usage.Memory;
                    metricsInfo.Add($"{pod.Metadata.Name}/{container.Name} - CPU: {cpuUsage}, Memory: {memoryUsage}");
                }
            }
            return metricsInfo;
        }

        public async Task<string> ReadCpuCoresForNodeAndPodMetrics()
        {
            try
            {
                var nodeList = await _kubernetesClient.ListNodeAsync();
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true }; // For pretty printing
                string nodeListJson = System.Text.Json.JsonSerializer.Serialize(nodeList, jsonOptions);

                if (nodeList.Items.Count > 0)
                {
                    if (nodeList.Items[0].Status.Capacity.TryGetValue("cpu", out var cpu))
                    {
                        decimal cpuValue = decimal.Parse(cpu.ToString());
                        this.cpuCores = (int)Math.Round(cpuValue);
                    }
                    if (nodeList.Items[0].Status.Capacity.TryGetValue("memory", out var memory))
                    {
                        this.totalMemory = float.Parse(K3sMetricsUtil.ConvertMemoryUsage(memory.ToString(), _k3sMetricsUtilLogger), CultureInfo.InvariantCulture);
                    }
                    return nodeList.Items[0].Metadata.Name;
                }
                else
                {
                    _logger.LogInformation("FetchNodeNameAsync() - No nodes found in the cluster.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception Details - ");
            }
            return "";
        }
    }

    // Define classes to deserialize the JSON response
    public class PodMetricsList
    {
        [JsonProperty("items")]
        public List<PodMetrics> Items { get; set; }
    }

    public class PodMetrics
    {
        [JsonProperty("metadata")]
        public Metadata Metadata { get; set; }

        [JsonProperty("containers")]
        public List<ContainerMetrics> Containers { get; set; }
    }

    public class ContainerMetrics
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("usage")]
        public Usage Usage { get; set; }
    }

    public class Usage
    {
        [JsonProperty("cpu")]
        public string Cpu { get; set; }

        [JsonProperty("memory")]
        public string Memory { get; set; }
    }

    public class Metadata
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}