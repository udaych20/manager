using Authlete.Dto;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using systems_manager.src.communication;
using systems_manager.src.service;

public class DeviceOfflineDataSyncSchduler
{
    private readonly InternetConnectionChecker icc = new();
    private Timer _timer;
    // application/database_file_path -> SM_SQLLITE_FILE_PATH
    private readonly string _localDBPath;
    private readonly ConfigPropertiesReader _configPropertiesReader;

    private string cloudCareIP;
    private string assetManagerPort;
    private string ipdaressPingForOnlineStatus;
    private readonly string deviceOfflineDataSyncDeviceMetricsTopic;
    private string deviceId;
    private string deviceOfflineDataSyncDeviceMetricsTopicClientId;

    private readonly string deviceOfflineDataSyncServiceMetricsTopic;
    private string deviceOfflineDataSyncServiceMetricsTopicClientId;
    private readonly DeviceOfflineSyncCloudCommunicationManager cloudCommMgr;
    private readonly ILogger<DeviceOfflineDataSyncSchduler> _logger;
    private readonly DatabaseService _databaseService;

    public DeviceOfflineDataSyncSchduler(
        ConfigPropertiesReader configPropertiesReader,
        ILoggerFactory loggerFactory,
        DatabaseService databaseService)
    {
        _configPropertiesReader = configPropertiesReader;
        _databaseService = databaseService;
        _localDBPath = _configPropertiesReader.GetPropertyValue("SM_SQLLITE_FILE_PATH");

        cloudCareIP = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
        assetManagerPort = _configPropertiesReader.GetPropertyValue("ASSET_MANAGER_PORT");
        ipdaressPingForOnlineStatus = "https://" + cloudCareIP  + "/" + _configPropertiesReader.GetPropertyValue("SM_DEVICE_STATUS_CHECK_URL");

        deviceOfflineDataSyncDeviceMetricsTopic = _configPropertiesReader.GetPropertyValue("SM_DEVICE_OFFLINE_SYNC_DEVICE_METRICS_TOPIC");
        deviceId = _configPropertiesReader.GetPropertyValue("DEVICE_ID");
        deviceOfflineDataSyncDeviceMetricsTopicClientId = "SM_D_OFFLNE_CLIENT_" + deviceId;

        deviceOfflineDataSyncServiceMetricsTopic = _configPropertiesReader.GetPropertyValue("SM_DEVICE_OFFLINE_SYNC_SERVICE_METRICS_TOPIC");
        deviceOfflineDataSyncServiceMetricsTopicClientId = "SM_D_SER_MET_OFF_SYNC_" + deviceId;

        cloudCommMgr = new DeviceOfflineSyncCloudCommunicationManager(_configPropertiesReader, loggerFactory);
        _logger = loggerFactory.CreateLogger<DeviceOfflineDataSyncSchduler>();
    }

    public void StartScheduler()
    {
        string frequencyProperty = _configPropertiesReader.GetPropertyValue("SM_DEVICE_OFFLINE_SYNC_SCHEDULAR_FREQUENCY");
        _logger.LogInformation("device_offline_data_sync_schedular_frequency - {FrequencyProperty}", frequencyProperty);
        if (int.TryParse(frequencyProperty, out int frequencySeconds))
        {
            _timer = new Timer(this.SyncOfflineData, null, TimeSpan.Zero, TimeSpan.FromSeconds(frequencySeconds));
        }
        else
        {
            throw new ArgumentException("Invalid frequency value in the properties file");
        }
    }

    private void SyncOfflineData(object state)
    {
        bool isAccessible = icc.IsInternetConnectedAsync(ipdaressPingForOnlineStatus).GetAwaiter().GetResult();
        _logger.LogInformation("internet connected? - {IsAccessible}", isAccessible);
        if (isAccessible)
        {
            SyncDeviceOfflineData();
            SyncOfflineServiceData();
        }
    }

    private void SyncOfflineServiceData()
    {
        // service_metrics/service_offline_sync_batch_size -> SM_SERVICE_METRICS_SERVICE_OFFLINE_SYNC_BATCH_SIZE
        int batchSizeService = int.Parse(_configPropertiesReader.GetPropertyValue("SM_SERVICE_METRICS_SERVICE_OFFLINE_SYNC_BATCH_SIZE"));
        int offsetService = 0;
        bool hasMoreDataService = true;

        // Ensure the connection string is properly formatted for Microsoft.Data.Sqlite
        string connectionString = $"Data Source={_localDBPath};";

        try
        {
            // Open the connection using the connection string
            using var connection = new SqliteConnection(connectionString);
            connection.Open(); // Explicitly open the connection if needed

            while (hasMoreDataService)
            {
                string clientIdString = $"{deviceOfflineDataSyncServiceMetricsTopicClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
                // Begin a database transaction
                using var transaction = connection.BeginTransaction();
                List<ServiceMetricsData> batchService = _databaseService.GetServiceMetricsBatch(batchSizeService, offsetService);
                try
                {
                    _logger.LogInformation("offline-device-sync batch count - {BatchServiceCount}", batchService.Count);

                    if (batchService.Count > 0)
                    {
                        string jsonArray = JsonConvert.SerializeObject(batchService);

                        // Attempt to publish the message
                        cloudCommMgr.PublishMessage(deviceOfflineDataSyncServiceMetricsTopic, clientIdString, jsonArray);
                        Thread.Sleep(3000);
                        // Update the database within the transaction
                        _databaseService.UpdateServiceDeleteSwitchForBatch(batchService, transaction);

                        // Commit the transaction if publish is successful
                        transaction.Commit();

                        DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                        var logInfo = new LogInformation
                        {
                            Topic = deviceOfflineDataSyncServiceMetricsTopic,
                            InternetConnected = status.status,
                            ClientId = clientIdString,
                            podName = status.RunningPodName,
                            Message = $"Messages Published {batchService.Count} and status updated",
                            Timestamp = DateTime.UtcNow
                        };
                        _databaseService.InsertLog(logInfo).GetAwaiter();

                        offsetService += batchService.Count;
                    }
                    else
                    {
                        hasMoreDataService = false;
                    }
                }
                catch (Exception ex)
                {
                    // Rollback the transaction in case of any exception
                    transaction.Rollback();
                    DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                    var logInfo = new LogInformation
                    {
                        Topic = deviceOfflineDataSyncServiceMetricsTopic,
                        InternetConnected = status.status,
                        ClientId = clientIdString,
                        podName = status.RunningPodName,
                        Message = $"transaction rolled back {batchService.Count}",
                        Exception = ex.ToString(),
                        Timestamp = DateTime.UtcNow
                    };
                    _databaseService.InsertLog(logInfo).GetAwaiter();
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("offline-device-sync error: {Error}", ex);
        }
        _ = _databaseService.DeleteServiceMarkedRecords();
    }

    private void SyncDeviceOfflineData()
    {
        // system_metrics/device_offline_sync_batch_size -> SM_SYSTEM_METRICS_DEVICE_OFFLINE_SYNC_BATCH_SIZE
        int batchSizeDevice = int.Parse(_configPropertiesReader.GetPropertyValue("SM_SYSTEM_METRICS_DEVICE_OFFLINE_SYNC_BATCH_SIZE"));
        int offsetDevice = 0;
        bool hasMoreDataDevice = true;

        // Prepare the connection string
        string connectionString = $"Data Source={_localDBPath};";

        try
        {
            // Initialize the connection with the connection string
            using var connection = new SqliteConnection(connectionString);
            // Explicitly open the connection
            connection.Open();

            while (hasMoreDataDevice)
            {
                // Begin a database transaction
                using var transaction = connection.BeginTransaction();
                string clientIdString = $"{deviceOfflineDataSyncDeviceMetricsTopicClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";

                try
                {
                    List<DeviceMetricsData> batchDevice = _databaseService.GetDeviceMetricsBatch(batchSizeDevice, offsetDevice);
                    _logger.LogInformation("offline-service-sync batch count - {BatchDeviceCount}", batchDevice.Count);

                    if (batchDevice.Count > 0)
                    {
                        string jsonArray = JsonConvert.SerializeObject(batchDevice);

                        // Attempt to publish the message
                        cloudCommMgr.PublishMessage(deviceOfflineDataSyncDeviceMetricsTopic, clientIdString, jsonArray);
                        Thread.Sleep(3000);
                        // Update the database within the transaction
                        _databaseService.UpdateDeviceDeleteSwitchForBatch(batchDevice, transaction);

                        // Commit the transaction if publish is successful
                        transaction.Commit();

                        DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                        var logInfo = new LogInformation
                        {
                            Topic = deviceOfflineDataSyncDeviceMetricsTopic,
                            InternetConnected = status.status,
                            ClientId = clientIdString,
                            podName = status.RunningPodName,
                            Message = $"Messages Published {batchSizeDevice} and status updated",
                            Timestamp = DateTime.UtcNow
                        };
                        _databaseService.InsertLog(logInfo).GetAwaiter();

                        offsetDevice += batchDevice.Count;
                    }
                    else
                    {
                        hasMoreDataDevice = false;
                    }
                }
                catch (Exception ex)
                {
                    // Rollback the transaction in case of any exception
                    transaction.Rollback();
                    DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
                    var logInfo = new LogInformation
                    {
                        Topic = deviceOfflineDataSyncServiceMetricsTopic,
                        InternetConnected = status.status,
                        ClientId = clientIdString,
                        podName = status.RunningPodName,
                        Message = $"transaction rolled back {batchSizeDevice}",
                        Exception = ex.ToString(),
                        Timestamp = DateTime.UtcNow
                    };
                    _databaseService.InsertLog(logInfo).GetAwaiter();
                    throw; // Re-throw the exception to handle it outside or log it
                }
            }
            _databaseService.DeleteDeviceMarkedRecords();
        }
        catch (Exception ex)
        {
            _logger.LogError("offline-device-sync error: {Error}", ex);
        }
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