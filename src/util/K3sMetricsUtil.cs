using System.Globalization;
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
            ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            var podMetricsList = JsonConvert.DeserializeObject<PodMetricsList>(json);
            
            var outputList = new List<ServiceMetricsData>();
            int totalRestartCount = 0;
            var podCountLookup = BuildPodCountLookup(podMetricsList);
            if (podMetricsList?.Items != null)
            {
                foreach (var item in podMetricsList.Items)
                {
                    if (item?.Containers == null)
                    {
                        continue;
                    }

                    foreach (var container in item.Containers)
                    {
                        var containerName = container?.Name;
                        if (string.IsNullOrWhiteSpace(containerName))
                        {
                            continue;
                        }

                        var lowerContainerName = containerName.ToLowerInvariant();
                        if (lowerContainerName.Contains("fluent-bit"))
                        {
                            continue;
                        }

                        if (lowerContainerName.Contains("systems-manager"))
                        {
                            DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                            if (status != null && status.RunningPodName != item.Metadata.Name)
                            {
                                status.RunningPodName = item.Metadata.Name;
                                _databaseService.UpdateDeviceConnectivityStatus(status);
                            }
                            continue;
                        }
                        var outputItem = new ServiceMetricsData
                        {
                            deviceId = deviceId,
                            timestamp = DateTime.UtcNow,
                            serviceName = containerName,
                            cpuUsage = ConvertCpuUsage(container.Usage.Cpu),
                            memoryUsage = ConvertMemoryUsage(container.Usage.Memory, _k3sMetricsUtilLogger),
                            podCount = GetPodCountForContainer(podCountLookup, item, container),
                            restartCount = totalRestartCount,
                            outputIO = 0,
                            inputIO = 0,
                            deleteSwitch = "N",
                            nodeName = item.Metadata.labels.nodeType,
                            nodeType = item.Metadata.labels.nodeType
                        };

                        outputList.Add(outputItem);
                    }

                }
            }
            return outputList;
        }

        private static Dictionary<string, int> BuildPodCountLookup(PodMetricsList podMetricsList)
        {
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (podMetricsList?.Items == null)
            {
                return lookup;
            }

            foreach (var item in podMetricsList.Items)
            {
                if (item == null)
                {
                    continue;
                }

                var serviceLabel = item.Metadata?.labels?.service;
                if (!string.IsNullOrEmpty(serviceLabel))
                {
                    if (!lookup.TryAdd(serviceLabel, 1))
                    {
                        lookup[serviceLabel]++;
                    }
                    continue;
                }

                if (item.Containers == null)
                {
                    continue;
                }

                foreach (var container in item.Containers)
                {
                    var containerName = container?.Name;
                    if (string.IsNullOrEmpty(containerName))
                    {
                        continue;
                    }

                    if (!lookup.TryAdd(containerName, 1))
                    {
                        lookup[containerName]++;
                    }
                }
            }

            return lookup;
        }

        private static int GetPodCountForContainer(Dictionary<string, int> podCountLookup, Item podMetricsItem, Container container)
        {
            if (podCountLookup == null || podMetricsItem == null || container == null)
            {
                return 0;
            }

            var serviceLabel = podMetricsItem.Metadata?.labels?.service;
            if (!string.IsNullOrEmpty(serviceLabel) && podCountLookup.TryGetValue(serviceLabel, out var serviceCount))
            {
                return serviceCount;
            }

            var containerName = container.Name;
            if (!string.IsNullOrEmpty(containerName) && podCountLookup.TryGetValue(containerName, out var containerCount))
            {
                return containerCount;
            }

            return 0;
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

        private const double BytesPerGigabyte = 1_000_000_000d;
        private const double BytesPerGibibyte = 1024d * 1024d * 1024d;
        private const double BytesPerMegabyte = 1_000_000d;

        private static bool TryParseQuantity(string quantity, out double value, out string unit)
        {
            value = 0;
            unit = string.Empty;

            if (string.IsNullOrWhiteSpace(quantity))
            {
                return false;
            }

            int index = 0;
            while (index < quantity.Length && (char.IsDigit(quantity[index]) || quantity[index] == '.' || quantity[index] == '-'))
            {
                index++;
            }

            if (index == 0)
            {
                return false;
            }

            string numberPart = quantity.Substring(0, index);
            unit = quantity.Substring(index);

            return double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static double ConvertCpuQuantityToCores(string cpuUsage)
        {
            if (!TryParseQuantity(cpuUsage, out var value, out var unit))
            {
                return 0d;
            }

            string normalizedUnit = string.IsNullOrEmpty(unit) ? string.Empty : unit.Trim().ToLowerInvariant();

            return normalizedUnit switch
            {
                "" => value,
                "n" => value / 1_000_000_000d,
                "u" => value / 1_000_000d,
                "m" => value / 1_000d,
                _ => value
            };
        }

        private static double ConvertMemoryQuantityToBytes(string memoryUsage)
        {
            if (!TryParseQuantity(memoryUsage, out var value, out var unit))
            {
                return 0d;
            }

            string normalizedUnit = string.IsNullOrEmpty(unit) ? string.Empty : unit.Trim().ToLowerInvariant();

            return normalizedUnit switch
            {
                "" => value,
                "ki" => value * 1024d,
                "mi" => value * Math.Pow(1024d, 2),
                "gi" => value * Math.Pow(1024d, 3),
                "ti" => value * Math.Pow(1024d, 4),
                "pi" => value * Math.Pow(1024d, 5),
                "ei" => value * Math.Pow(1024d, 6),
                "k" => value * 1000d,
                "m" => value * Math.Pow(1000d, 2),
                "g" => value * Math.Pow(1000d, 3),
                "t" => value * Math.Pow(1000d, 4),
                "p" => value * Math.Pow(1000d, 5),
                "e" => value * Math.Pow(1000d, 6),
                _ => value
            };
        }

        public static string ConvertCpuUsage(string cpuUsage)
        {
            if (string.IsNullOrWhiteSpace(cpuUsage))
            {
                return 0d.ToString("F2", CultureInfo.InvariantCulture);
            }

            double coresUsed = ConvertCpuQuantityToCores(cpuUsage);
            return coresUsed.ToString("F2", CultureInfo.InvariantCulture);
        }

        public static string ConvertMemoryUsage(string memoryUsage, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            double bytes = ConvertMemoryQuantityToBytes(memoryUsage);
            double memoryInMegabytes = bytes / BytesPerMegabyte;

            _k3sMetricsUtilLogger?.LogInformation("Node MemoryUsage - {MemoryUsage} converted into {MemoryInMb} MB", memoryUsage, memoryInMegabytes);

            return memoryInMegabytes.ToString("F2", CultureInfo.InvariantCulture);
        }


        public static float ConvertMemoryUsagePct(string memoryUsage, float totalMemory, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            if (totalMemory <= 0)
            {
                return 0f;
            }

            double memoryInMegabytes = ConvertMemoryQuantityToBytes(memoryUsage) / BytesPerMegabyte;
            return (float)(memoryInMegabytes / totalMemory * 100d);
        }

        public static float ConvertMemoryUsageKibIntoGB(string memoryUsage, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            double bytes = ConvertMemoryQuantityToBytes(memoryUsage);
            if (bytes <= 0)
            {
                return 0f;
            }

            double gigabytes = bytes / BytesPerGigabyte;
            _k3sMetricsUtilLogger?.LogInformation("Node MemoryUsage - {MemoryUsage} converted into {MemoryInGb} GB", memoryUsage, gigabytes);
            return (float)gigabytes;
        }

        public static float CalculateMemoryUsagePercentage(float usedMemoryGB, float totalMemoryGB)
        {
            return usedMemoryGB / totalMemoryGB * 100;
        }

        public static string CalculateCpuUsageNodePercentage(string cpuUsage, int totalCores, ILogger<K3sMetricsUtil> _k3sMetricsUtilLogger)
        {
            double coresUsed = ConvertCpuQuantityToCores(cpuUsage);
            double percentageUsed = totalCores > 0 ? coresUsed / totalCores * 100d : 0d;

            string cpu = percentageUsed.ToString("F2", CultureInfo.InvariantCulture);
            _k3sMetricsUtilLogger?.LogInformation("cpu usage {CpuUsage} received from node metrics and converted into {CpuPercentage}", cpuUsage, cpu);
            return cpu;
        }
    }

}
