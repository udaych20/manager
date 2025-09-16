using SQLite;
using System.Data;
using Dapper;
using System.Text.Json;
using ServiceUsageMonitor;


namespace systems_manager.src.service
{
    public class DatabaseService
    {
        private readonly SQLiteConnection connection;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(string _dbPath, ILoggerFactory _loggerFactory)
        {
            connection = new SQLiteConnection(_dbPath);
            _logger = _loggerFactory.CreateLogger<DatabaseService>();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            connection.CreateTable<DeviceMetricsData>();
            connection.CreateTable<ServiceMetricsData>();
            connection.CreateTable<DeviceConnectivityStatus>();
            connection.CreateTable<LogInformation>();
            connection.CreateTable<Service>();
            connection.CreateTable<DeviceMonitoringStatus>();
            connection.CreateTable<ConfigurationDetails>();

            connection.CreateTable<NodeMetrics>();
            connection.CreateTable<ServiceMetrics>();
            connection.CreateTable<EventLog>();

            var count = connection.Table<DeviceConnectivityStatus>().Count();
            if (count == 0)
            {
                var defaultStatus = new DeviceConnectivityStatus
                {
                    Id = 1,
                    status = "Disconnected",
                    timestamp = DateTime.UtcNow
                };
                connection.Insert(defaultStatus);
                _logger.LogDebug("Default DeviceConnectivityStatus Disconnected inserted successfully.");
            }
            else
            {
                _logger.LogDebug("DeviceConnectivityStatus entry already exists. Skipping default.");
            }


            // Insert a sample EventLog record
            var eventLogCount = connection.Table<EventLog>().Count();
            if (eventLogCount == 0)
            {
                var sampleEventLog = new EventLog
                {
                    PolicyName = "SamplePolicy",
                    ServiceName = "SampleService",
                    ActionTaken = "NOTIFIED", // Assuming ActionTaken is an enum you have defined elsewhere
                    Timestamp = DateTime.UtcNow,
                    Details = "{\"detailKey\": \"detailValue\"}" // Example JSON format details
                };
                connection.Insert(sampleEventLog);
                _logger.LogDebug("Sample EventLog record inserted successfully.");
            }
            else
            {
                _logger.LogDebug("EventLog entry already exists. Skipping sample record.");
            }
        }

        public void InsertDeviceMetrics(DeviceMetricsData data)
        {
            connection.Insert(data);
            _logger.LogDebug("DeviceMetricsData inserted successfully. id - {Id}", data.Id);
        }

        public void InsertServiceMetrics(ServiceMetricsData data)
        {
            connection.Insert(data);
            _logger.LogDebug("ServiceMetricsData inserted successfully. id - {Id}", data.Id);
        }

        public void InsertDeviceConnectivityStatus(DeviceConnectivityStatus status)
        {
            connection.Insert(status);
            _logger.LogDebug("DeviceConnectivityStatus inserted successfully. id - {Id}", status.Id);
        }

        public List<DeviceConnectivityStatus> GetAllStatuses()
        {
            return [.. connection.Table<DeviceConnectivityStatus>()];
        }

        public DeviceConnectivityStatus GetDeviceConnectivityStatusById(int id)
        {
            return connection.Table<DeviceConnectivityStatus>().FirstOrDefault(s => s.Id == id);
        }

        public void UpdateDeviceConnectivityStatus(DeviceConnectivityStatus status)
        {
            connection.Update(status);
            _logger.LogDebug("DeviceConnectivityStatus updated successfully. status - {Status}", status.status);
        }

        public void DeleteDeviceConnectivityStatus(int id)
        {
            connection.Delete<DeviceConnectivityStatus>(id);
            _logger.LogDebug("DeviceConnectivityStatus deleted successfully. id - {Id}", id);
        }

        public List<DeviceMetricsData> GetAllDeviceMetrics()
        {
            return [.. connection.Table<DeviceMetricsData>()];
        }

        public List<ServiceMetricsData> GetAllServiceMetrics()
        {
            return [.. connection.Table<ServiceMetricsData>()];
        }

        public void DeleteAllDeviceMetrics()
        {
            connection.DeleteAll<DeviceMetricsData>();
            _logger.LogDebug("All DeviceMetricsData deleted successfully.");
        }

        public void DeleteAllServiceMetrics()
        {
            connection.DeleteAll<ServiceMetricsData>();
            _logger.LogDebug("All ServiceMetricsData deleted successfully.");
        }

        public List<DeviceMetricsData> GetDeviceMetricsBatch(int batchSize, int offset)
        {
            return [.. connection.Table<DeviceMetricsData>()
                            .Where(metric => metric.deleteSwitch == "N")
                            .Skip(offset)
                            .Take(batchSize)];
        }

        // Method to get device metrics in batches
        public List<ServiceMetricsData> GetServiceMetricsBatch(int batchSize, int offset)
        {
            return [.. connection.Table<ServiceMetricsData>()
                            .Where(metric => metric.deleteSwitch == "N")
                            .Skip(offset)
                            .Take(batchSize)];
        }

        public void UpdateDeviceDeleteSwitchForBatch(List<DeviceMetricsData> batch, IDbTransaction transaction)
        {
            foreach (var item in batch)
            {
                item.deleteSwitch = "Y";
                // Assuming a method extension or similar for Update, as Dapper does not provide an Update method out-of-the-box
                transaction.Connection.Execute("UPDATE DeviceMetricsData SET deleteSwitch = @DeleteSwitch WHERE Id = @Id",
                                      new { DeleteSwitch = item.deleteSwitch, item.Id }, transaction);
            }
        }

        public void UpdateServiceDeleteSwitchForBatch(List<ServiceMetricsData> batch, IDbTransaction transaction)
        {
            foreach (var item in batch)
            {
                item.deleteSwitch = "Y";
                // Assuming a method extension or similar for Update, as Dapper does not provide an Update method out-of-the-box
                transaction.Connection.Execute("UPDATE ServiceMetricsData SET deleteSwitch = @DeleteSwitch WHERE Id = @Id",
                                      new { DeleteSwitch = item.deleteSwitch, item.Id }, transaction);
            }
        }

        public void DeleteDeviceMarkedRecords()
        {
            // Fetch all records where deleteSwitch is "Y"
            var DeviceRecordsToDelete = connection.Table<DeviceMetricsData>().Where(x => x.deleteSwitch == "Y").ToList();
            foreach (var record in DeviceRecordsToDelete)
            {
                connection.Delete(record);
            }
        }

        public async Task DeleteServiceMarkedRecords()
        {
            var serviceRecordsToDelete = connection.Table<ServiceMetricsData>().Where(x => x.deleteSwitch == "Y").ToList();

            foreach (var record in serviceRecordsToDelete)
            {
                connection.Delete(record);
            }

        }

        public void DeleteLog(int id)
        {
            connection.Delete<LogInformation>(id);
        }

        public void DeleteLog()
        {
            connection.DeleteAll<LogInformation>();
        }

        public void UpdateLog(int id, string newMessage)
        {
            var logInformation = connection.Find<LogInformation>(id);
            if (logInformation != null)
            {
                logInformation.Message = newMessage;
                logInformation.Timestamp = DateTime.UtcNow; // Optionally update the timestamp
                connection.Update(logInformation);
            }
        }

        public IEnumerable<LogInformation> GetAllLogs()
        {
            return [.. connection.Table<LogInformation>()];
        }

        public LogInformation GetLogById(int id)
        {
            return connection.Find<LogInformation>(id);
        }

        public async Task InsertLog(LogInformation logInfo)
        {
            connection.Insert(logInfo);
        }

        public void CreateService(Service service)
        {
            connection.Insert(service);
        }

        public Service GetServiceById(int id)
        {
            return connection.Table<Service>().FirstOrDefault(s => s.Id == id);
        }

        public Service GetServiceByServieName(string serviceName)
        {
            return connection.Table<Service>().FirstOrDefault(s => s.ServiceName == serviceName);
        }

        public List<Service> GetAllServices()
        {
            return [.. connection.Table<Service>()];
        }

        public void UpdateService(Service service)
        {
            connection.Update(service);
        }

        public void DeleteService(int id)
        {
            var service = GetServiceById(id);
            if (service != null)
            {
                connection.Delete(service);
            }
        }

        public List<DeviceMonitoringStatus> GetAllDeviceMonitoringStatus()
        {
             return [.. connection.Table<DeviceMonitoringStatus>()];
        }


        public void InsertDeviceMonitoringStatus(DeviceMonitoringStatus deviceMonitoringStatus)
        {
            connection.Insert(deviceMonitoringStatus);
        }


        public void InsertDeviceMonitoringStatus(bool isDeviceResourceUsageHigh)
        {
            var DeviceMonitoringStatusObj = new DeviceMonitoringStatus()
            {
                isDeviceResourceUsageHigh = isDeviceResourceUsageHigh,
                timestamp = DateTime.Now
            };
            connection.Insert(DeviceMonitoringStatusObj);
        }


        public void DeleteAllDeviceMonitoringStatus()
        {
            connection.DeleteAll<DeviceMonitoringStatus>();
        }


        public void UpdateDeviceMonitoringStatus(DeviceMonitoringStatus deviceMonitoringStatus)
        {
            connection.Update(deviceMonitoringStatus);
        }

        public List<DeviceMonitoringStatus> GetAllDeviceMonitoringStatusByDurationAndStatus(DateTime startTime, DateTime endTime, bool status)
        {
            var result = connection.Table<DeviceMonitoringStatus>()
                                   .Where(dms => dms.timestamp >= startTime && dms.timestamp <= endTime && dms.isDeviceResourceUsageHigh == status)
                                   .ToList();
            return result;
        }

        // Method to get all DeviceMonitoringStatus records from the last 60 seconds by status
        public bool IsDeviceIsNormalStateInGivenDuration(int secs)
        {
            // Calculate the start time as the current time minus 60 seconds
            var startTime = DateTime.UtcNow.AddSeconds(-secs);
            // The end time is the current time
            var endTime = DateTime.UtcNow;

            // Use LINQ to query the SQLite table for records within the last 60 seconds and with the specified status
            var result = connection.Table<DeviceMonitoringStatus>()
                                   .Where(dms => dms.timestamp >= startTime && dms.timestamp <= endTime)
                                   .ToList();
            foreach (var dms in result)
            {
                // Serialize the object to a JSON string
                // If using System.Text.Json
                var json = JsonSerializer.Serialize(dms);
                // If using Newtonsoft.Json, use: var json = JsonConvert.SerializeObject(dms);

                // Log the serialized object
                _logger.LogDebug($"DeviceMonitoringStatus: {json}");
            }

            // Log the count of DeviceMonitoringStatus objects
            _logger.LogDebug($"Count of DeviceIsNormalStateInGivenDuration: {result.Count}");            // Check if ALl the record has isDeviceNormalState as true
            return result.All(dms => !dms.isDeviceResourceUsageHigh);
        }

        public void InsertConfigurationDetails(ConfigurationDetails details)
        {
            try
            {
                connection.Insert(details);
                _logger.LogDebug("ConfigurationDetails inserted successfully. Key - {Key}", details.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting ConfigurationDetails with Key {Key}", details.Key);
            }
        }

        public ConfigurationDetails GetConfigurationDetailsByKey(string key)
        {
            try
            {
                return connection.Table<ConfigurationDetails>().FirstOrDefault(cd => cd.Key == key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ConfigurationDetails with Key {Key}", key);
                return null;
            }
        }

        public List<ConfigurationDetails> GetAllConfigurationDetails()
        {
            try
            {
                return connection.Table<ConfigurationDetails>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all ConfigurationDetails");
                return new List<ConfigurationDetails>();
            }
        }


        public bool UpdateConfigurationDetails(ConfigurationDetails details)
        {
            try
            {
                var existing = GetConfigurationDetailsByKey(details.Key);
                if (existing != null)
                {
                    connection.Update(details);
                    _logger.LogDebug("ConfigurationDetails updated successfully. Key - {Key}", details.Key);
                    return true;
                }
                else
                {
                    _logger.LogWarning("ConfigurationDetails with Key {Key} not found for update.", details.Key);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ConfigurationDetails with Key {Key}", details.Key);
                return false;
            }
        }

        public bool DeleteConfigurationDetails(string key)
        {
            try
            {
                var details = GetConfigurationDetailsByKey(key);
                if (details != null)
                {
                    connection.Delete(details);
                    _logger.LogDebug("ConfigurationDetails deleted successfully. Key - {Key}", key);
                    return true;
                }
                else
                {
                    _logger.LogWarning("ConfigurationDetails with Key {Key} not found for deletion.", key);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ConfigurationDetails with Key {Key}", key);
                return false;
            }
        }

        public void InsertNodeMetrics(NodeMetrics data)
        {
            connection.Insert(data);
            _logger.LogDebug("NodeMetrics inserted successfully. id - {Id}", data.Id);
        }

        public List<NodeMetrics> GetAllNodeMetrics()
        {
            try
            {
                return connection.Table<NodeMetrics>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all NodeMetrics");
                return new List<NodeMetrics>();
            }

        }

        public NodeMetrics GetNodeMetricsByNodeType(string nodeType,string resourceName)
        {
            try
            {
                _logger.LogDebug("GetNodeMetricsByNodeType {nodeType} , {resourceName}", nodeType,resourceName);
                // Filter records based on Timestamp being within the specified duration
                return connection.Table<NodeMetrics>()
                                .FirstOrDefault(m => m.NodeType == nodeType && m.ResourceName == resourceName);
                                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all NodeMetrics");
                return new NodeMetrics();
            }
        }


        public List<NodeMetrics> GetNodeMetricsByMinutes(int minutesBack)
        {
            try
            {
                // Calculate startTime based on minutesBack
                DateTime endTime = DateTime.Now;
                DateTime startTime = endTime.AddMinutes(-minutesBack);

                // Filter records based on Timestamp being within the specified duration
                var metrics = connection.Table<NodeMetrics>()
                                .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                                .ToList();
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ServiceMetrics by minutes back");
                return new List<NodeMetrics>();
            }
        }

        public bool UpdateNodeMetrics(NodeMetrics data)
        {
            try
            {
                var affectedRows = connection.Update(data);
                if (affectedRows > 0)
                {
                    _logger.LogDebug("NodeMetrics updated successfully. id - {Id}", data.Id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("NodeMetrics with id {Id} not found for update.", data.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating NodeMetrics with id {Id}", data.Id);
                return false;
            }
        }

        public bool DeleteNodeMetrics(int id)
        {
            try
            {
                var data = connection.Table<NodeMetrics>().FirstOrDefault(nm => nm.Id == id);
                if (data != null)
                {
                    connection.Delete(data);
                    _logger.LogDebug("NodeMetrics deleted successfully. id - {Id}", id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("NodeMetrics with id {Id} not found for deletion.", id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting NodeMetrics with id {Id}", id);
                return false;
            }
        }

        public void InsertServiceMetrics(ServiceMetrics data)
        {
            connection.Insert(data);
            _logger.LogDebug("ServiceMetrics inserted successfully. id - {Id}", data.Id);
        }

        public List<ServiceMetrics> GetAllNodeServiceMetrics()
        {
            try
            {
                return connection.Table<ServiceMetrics>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all ServiceMetrics");
                return new List<ServiceMetrics>();
            }
        }

        public List<ServiceMetrics> GetNodeServiceMetricsByMinutes(int minutesBack)
        {
            try
            {
                // Calculate startTime based on minutesBack
                DateTime endTime = DateTime.Now;
                DateTime startTime = endTime.AddMinutes(-minutesBack);

                // Filter records based on Timestamp being within the specified duration
                var metrics = connection.Table<ServiceMetrics>()
                                .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
                                .ToList();
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ServiceMetrics by minutes back");
                return new List<ServiceMetrics>();
            }
        }

        public bool UpdateServiceMetrics(ServiceMetrics data)
        {
            try
            {
                var affectedRows = connection.Update(data);
                if (affectedRows > 0)
                {
                    _logger.LogDebug("ServiceMetrics updated successfully. id - {Id}", data.Id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("ServiceMetrics with id {Id} not found for update.", data.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ServiceMetrics with id {Id}", data.Id);
                return false;
            }
        }

        public bool DeleteServiceMetrics(int id)
        {
            try
            {
                var data = connection.Table<ServiceMetrics>().FirstOrDefault(sm => sm.Id == id);
                if (data != null)
                {
                    connection.Delete(data);
                    _logger.LogDebug("ServiceMetrics deleted successfully. id - {Id}", id);
                    return true;
                }
                else
                {
                    _logger.LogWarning("ServiceMetrics with id {Id} not found for deletion.", id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ServiceMetrics with id {Id}", id);
                return false;
            }
        }

        public void InsertEventLog(EventLog logEntry)
        {
            connection.Insert(logEntry);
            _logger.LogDebug("EventLog inserted successfully. LogId - {LogId}", logEntry.LogId);
        }

        public EventLog GetEventLogById(int logId)
        {
            try
            {
                return connection.Table<EventLog>().FirstOrDefault(log => log.LogId == logId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching EventLog with LogId {LogId}", logId);
                return null;
            }
        }

        public List<EventLog> GetAllEventLogs()
        {
            try
            {
                return connection.Table<EventLog>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all EventLogs");
                return new List<EventLog>();
            }
        }

        public bool UpdateEventLog(EventLog logEntry)
        {
            try
            {
                var affectedRows = connection.Update(logEntry);
                if (affectedRows > 0)
                {
                    _logger.LogDebug("EventLog updated successfully. LogId - {LogId}", logEntry.LogId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("EventLog with LogId {LogId} not found for update.", logEntry.LogId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating EventLog with LogId {LogId}", logEntry.LogId);
                return false;
            }
        }

        public bool DeleteEventLog(int logId)
        {
            try
            {
                var logEntry = GetEventLogById(logId);
                if (logEntry != null)
                {
                    connection.Delete(logEntry);
                    _logger.LogDebug("EventLog deleted successfully. LogId - {LogId}", logId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("EventLog with LogId {LogId} not found for deletion.", logId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting EventLog with LogId {LogId}", logId);
                return false;
            }
        }

        internal ServiceMetrics GetNodeWiseServiceMetricsBynodeTypeAndResourceName(string nodeType, string resourceName)
        {
            try
            {
                return connection.Table<ServiceMetrics>()
                                .Where(m => m.NodeName == nodeType && m.ResourceName == resourceName).FirstOrDefault();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all NodeMetrics");
                return new ServiceMetrics();
            }
        }

        internal List<EventLog> GetAllEventLogsByServiceName(string serviceName)
        {
            return connection.Table<EventLog>()
                               .Where(m => m.ServiceName == serviceName).ToList();
        }

        internal List<EventLog> GetAllEventLogsByServiceNameAndPolicyName(string serviceName, string policyName)
        {
            return connection.Table<EventLog>()
                               .Where(m => m.ServiceName == serviceName && m.PolicyName == policyName).ToList();
        }

        internal List<EventLog> GetAllEventLogsByPolicyName(string policyName)
        {
            return connection.Table<EventLog>()
                               .Where(m => m.PolicyName == policyName).ToList();
        }

        internal List<EventLog> GetAllEventLogsByPolicyNameAndActionTaken(string policyName,string actionTaken)
        {
            return connection.Table<EventLog>()
                               .Where(m => m.PolicyName == policyName && m.ActionTaken == actionTaken).ToList();
        }


        public Task InsertEventLogAsync(EventLog logEntry)
        {
            return Task.Run(() =>
            {
                try
                {
                    connection.Insert(logEntry);
                    _logger.LogInformation("EventLog inserted successfully. LogId - {LogId}", logEntry.LogId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting EventLog");
                    throw;
                }
            });
        }

        public Task<bool> UpdateEventLogAsync(EventLog logEntry)
        {
            return Task.Run(() =>
            {
                try
                {
                    var affectedRows = connection.Update(logEntry);
                    if (affectedRows > 0)
                    {
                        _logger.LogDebug("EventLog updated successfully. LogId - {LogId}", logEntry.LogId);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("EventLog with LogId {LogId} not found for update.", logEntry.LogId);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating EventLog with LogId {LogId}", logEntry.LogId);
                    throw;
                }
            });
        }


        public Task<EventLog> GetEventLogByIdAsync(int logId)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Using LINQ to query the SQLite database
                    var logEntry = connection.Table<EventLog>().FirstOrDefault(log => log.LogId == logId);
                    if (logEntry != null)
                    {
                        _logger.LogDebug("EventLog fetched successfully for LogId - {LogId}", logId);
                    }
                    else
                    {
                        _logger.LogWarning("EventLog with LogId {LogId} not found.", logId);
                    }
                    return logEntry;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching EventLog with LogId {LogId}", logId);
                    throw;
                }
            });
        }

        public void StoreServatizationMessage(UnacknowledgedMessageDto data)
        {
            connection.Insert(data);
            Console.WriteLine("data - " + GetAllUnacknowledgedMessages());
        }

        public void DeleteServatizationMessage(UnacknowledgedMessageDto data)
        {
            connection.Delete(data);
        }

        public List<UnacknowledgedMessageDto> GetAllUnacknowledgedMessages()
        {
            return connection.Table<UnacknowledgedMessageDto>().ToList();
        }

        internal void StoreServiceUsageDto(ServiceUsageDto serviceUsage)
        {
            throw new NotImplementedException();
        }
    }


}
