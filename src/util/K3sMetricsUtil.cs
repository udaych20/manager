using k8s;
using Newtonsoft.Json;
using systems_manager.src.service;

namespace systems_manager.src.util
{
    public class K3sMetricsUtil
    {
        public static List<ServiceMetricsData> ReadMetrics(string json,
            long deviceId,
            PrometheusMetricsService prometheusMetricsService,
            DatabaseService _databaseService,
            ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger,
            Kubernetes _kubernetesClient,string namespaceParameter)
        {
            var podMetricsList = JsonConvert.DeserializeObject<PodMetricsList>(json);
            
            var outputItem = new ServiceMetricsData();
            var outputList = new List<ServiceMetricsData>();
            int totalRestartCount = 0;
            if (podMetricsList?.Items != null)
            {
                foreach (var item in podMetricsList.Items)
                {
                    outputItem = new ServiceMetricsData();
                    foreach (var container in item.Containers)
                    {
                        if (container.Name.ToLower().Contains("fluent-bit"))
                        {
                            continue;
                        }
                        else if (container.Name.ToLower().Contains("systems-manager"))
                        {
                            DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                            if (status != null && status.RunningPodName != item.Metadata.Name)
                            {
                                status.RunningPodName = item.Metadata.Name;
                                _databaseService.UpdateDeviceConnectivityStatus(status);
                            }
                            continue;
                        }
                        //UpdateNewServiceIntoServiceTable(_databaseService, container);
                        //string inputIOQuery = "rate(container_network_receive_bytes_total{pod=~" + item.Metadata.Name + "}[1m])";
                        //string inputIOQuery = $"rate(container_network_receive_bytes_total{{pod=~\"{item.Metadata.Name}\"}}[5m])";
                        //MetricResult inputIO = prometheusMetricsService.QueryMetricsAsync(inputIOQuery).GetAwaiter().GetResult();

                        //string outputIOQuery = "rate(container_network_transmit_bytes_total{pod=~" + item.Metadata.Name + "}[1m])";
                        //string outputIOQuery = $"rate(container_network_transmit_bytes_total{{pod=~\"{item.Metadata.Name}\"}}[5m])";
                        //MetricResult outputIO = prometheusMetricsService.QueryMetricsAsync(outputIOQuery).GetAwaiter().GetResult();
                        //outputItem = new ServiceMetricsData();
                        outputItem.deviceId = deviceId;
                        //outputItem.timestamp = item.Timestamp;outputItem.timestamp = item.Timestamp;
                        outputItem.timestamp = DateTime.UtcNow;
                        outputItem.serviceName = container.Name;
                        outputItem.cpuUsage = ConvertCpuUsage(container.Usage.Cpu);
                        outputItem.memoryUsage = ConvertMemoryUsage(container.Usage.Memory, _k3sMetricsUtilLogger);
                        outputItem.podCount = GetPodCountFromDeploymentSync(_kubernetesClient, namespaceParameter, container.Name.ToString()+"-deployment");
                        outputItem.restartCount = totalRestartCount;
                        outputItem.outputIO = 0;
                        outputItem.inputIO = 0;
                        outputItem.deleteSwitch = "N";
                        //TODO: Naresh - update to node name
                        outputItem.nodeName = item.Metadata.labels.nodeType;
                        //outputItem.nodeName = item.Spec.NodeName;
                        outputItem.nodeType = item.Metadata.labels.nodeType;
                        outputList.Add(outputItem);
                    }

                }
            }
            return outputList;
        }

        private static int GetPodCountFromDeploymentSync(k8s.Kubernetes _kubernetesClient, string namespaceName, string deploymentName)
        {
            try
            {
                // Get the deployment synchronously.
                var deploymentTask = _kubernetesClient.ReadNamespacedDeploymentAsync(deploymentName, namespaceName);
                var deployment = deploymentTask.GetAwaiter().GetResult();
                if (deployment == null)
                {
                    Console.WriteLine("Deployment not found.");
                    return 0;
                }

                // Get the pod list synchronously.
                var podsTask = _kubernetesClient.ListNamespacedPodAsync(namespaceName, labelSelector: ConvertLabelsToString(deployment.Spec.Selector.MatchLabels));
                var pods = podsTask.GetAwaiter().GetResult();
                return pods.Items.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
                return 0;
            }
        }

        // Helper method to convert label dictionary to a label selector string.
        private static string ConvertLabelsToString(IDictionary<string, string> labels)
        {
            return string.Join(",", labels.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        private static void UpdateNewServiceIntoServiceTable(DatabaseService _databaseService, Container container)
        {
            Service service = _databaseService.GetServiceByServieName(container.Name);
            if (service == null)
            {
                var newService = new Service
                {
                    ServiceName = container.Name,
                    Mode = "device",
                    Priority = 50,
                    Status = "active",
                    Category = "non-critical"
                };
                if (!newService.ServiceName.Equals("health-score-service"))
                {
                    newService.Category = "critical";
                }
                _databaseService.CreateService(newService);
            }
            else
            {   // once scale down from cloud it will again update the mode as device and status active in serive table
               
                service.Status = "active";
                if(service.Mode.Equals("cloud"))
                {
                    return;
                }
                service.Mode = "device";
                _databaseService.UpdateService(service);
            }
        }

        static K3sServiceMetricDto ConvertToRootObject(dynamic customObjectResult)
        {
            // Serialize the dynamic object to a JSON string
            string jsonString = JsonConvert.SerializeObject(customObjectResult);

            // Deserialize the JSON string back into the RootObject type
            K3sServiceMetricDto rootObject = JsonConvert.DeserializeObject<K3sServiceMetricDto>(jsonString);

            return rootObject;
        }

        public static string ConvertCpuUsage(string cpuUsage)
        {
            // Assuming the input is always in nanocores (n)
            if (cpuUsage.EndsWith("n"))
            {
                long cpuNanocores = long.Parse(cpuUsage.TrimEnd('n'));
                double cpuPercentage = (double)cpuNanocores / 1_000_000_000 * 100;
                return $"{cpuPercentage:F2}";
            }
            return "0";
        }

        public static string ConvertMemoryUsage(string memoryUsage, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            long value = long.Parse(memoryUsage.Substring(0, memoryUsage.Length - 2));
            string unit = memoryUsage.Substring(memoryUsage.Length - 2);
            _k3sMetricsUtilLogger.LogInformation($"Node MemoryUsage - {memoryUsage} converted into {unit}");

            switch (unit)
            {
                case "Ki":
                    string convertedVal = $"{(double) value / 1024:F2}";
                    return convertedVal;
                case "Mi":
                    return $"{value:F2}";
                case "Gi":
                    return $"{value * 1024:F2}";
                default:
                    return "0";
            }
        }


        public static float ConvertMemoryUsagePct(string memoryUsage, float totalMemory, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            long value = long.Parse(memoryUsage.Substring(0, memoryUsage.Length - 2));
            string unit = memoryUsage.Substring(memoryUsage.Length - 2);
            string memoryUsageCvt = "0";
            switch (unit)
            {
                case "Ki":
                    string convertedVal = $"{(double) value / 1024:F2}";
                    memoryUsageCvt = convertedVal;
                    break;
                case "Mi":
                    memoryUsageCvt = $"{value:F2}";
                    break;
                case "Gi":
                    memoryUsageCvt = $"{value * 1024:F2}";
                    break;
                default:
                    memoryUsageCvt = "0";
                    break;
            }
            return float.Parse(memoryUsageCvt) / totalMemory * 100;
        }

        public static float ConvertMemoryUsageKibIntoGB(string memoryUsage, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            long value = long.Parse(memoryUsage.Substring(0, memoryUsage.Length - 2));
            string unit = memoryUsage.Substring(memoryUsage.Length - 2);

            if (unit.Equals("Ki"))
            {
                string convertedVal = $"{(double)value * 1024:F2}";
                float gigabytes = float.Parse(convertedVal) / 1_000_000_000;
                _k3sMetricsUtilLogger.LogInformation($"Node MemoryUsage - {memoryUsage} converted into {gigabytes}");
                return gigabytes;
            }
            return 0;
        }

        public static float CalculateMemoryUsagePercentage(float usedMemoryGB, float totalMemoryGB)
        {
            return usedMemoryGB / totalMemoryGB * 100;
        }

        public static string CalculateCpuUsageNodePercentage(string cpuUsage, int totalCores, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            long cpuNanocores = 0;
            if (cpuUsage.EndsWith("n"))
            {
                cpuNanocores = long.Parse(cpuUsage.TrimEnd('n'));

            }
            // Convert nanocores to cores
            double coresUsed = cpuNanocores / 1_000_000_000.0;

            // Calculate the percentage of total cores used
            double percentageUsed = coresUsed / totalCores * 100;

            string cpu = $"{percentageUsed:F2}";
            _k3sMetricsUtilLogger.LogInformation("cpu usage {cpuUsage} reveived from node metrics and .converted into {cpu}", cpuUsage, cpu);
            return cpu;
        }
    }

}
