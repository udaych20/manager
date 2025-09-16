using ServiceManager;
using systems_manager.src.communication;
using systems_manager.src.schdulers;
using systems_manager.src.service;

namespace systems_manager.src.agent
{
    internal class SystemsManagerAgent
    {

        private readonly ILogger<SystemsManagerAgent> _logger;

        private readonly ConfigPropertiesReader _configProperties;

        private readonly ILoggerFactory _loggerFactory;

        private DatabaseService _databaseService;

        internal SystemsManagerAgent(ConfigPropertiesReader configProperties,
                                     ILoggerFactory _loggerFactory,
                                     DatabaseService _databaseService)
        {
            _configProperties = configProperties;
            _logger = _loggerFactory.CreateLogger<SystemsManagerAgent>();
            this._loggerFactory = _loggerFactory;
            this._databaseService = _databaseService;
        }

        internal void Start()
        {
            Start(_databaseService);
        }

        internal void Start(DatabaseService _databaseService)
        {
            _logger.LogInformation("SystemsManagerAgent start");

            try
            {
                // is_system_metrics_required -> SM_DEVICE_METRICS_IS_SYSTEM_METRICS_REQUIRED
                string IsSystemMetricsRequired = _configProperties.GetPropertyValue("SM_DEVICE_METRICS_IS_SYSTEM_METRICS_REQUIRED");
                captureTheSystemMetrics(_databaseService, IsSystemMetricsRequired);

                // is_service_metrics_required -> SM_IS_SERVICE_METRICS_REQUIRED
                string IsServiceMetricsRequired = _configProperties.GetPropertyValue("SM_IS_SERVICE_METRICS_REQUIRED");
                captureTheServiceMetrics(_databaseService, IsServiceMetricsRequired);

                // is_sensor_heartbeat_required -> SM_IS_SENSOR_HEARTBEAT_REQUIRED
                string IsSensorHeartBeatRequired = _configProperties.GetPropertyValue("SM_IS_SENSOR_HEARTBEAT_REQUIRED");
                captureTheSensorHeartBeats(_databaseService, IsSensorHeartBeatRequired);

                // device_offline_status_check_enabled -> SM_DEVICE_OFFLINE_STATUS_CHECK
                string deviceOfflineStatusCheckEnabled = _configProperties.GetPropertyValue("SM_DEVICE_OFFLINE_STATUS_CHECK");
                captureTheOfflineStatus(_databaseService, deviceOfflineStatusCheckEnabled);

                // is_device_offline_sync_required -> SM_IS_DEVICE_OFFLINE_SYNC_REQUIRED
                string IsDeviceOfflineSyncRequired = _configProperties.GetPropertyValue("SM_IS_DEVICE_OFFLINE_SYNC_REQUIRED");
                startTheDeviceOfflineSyncEnabled(_databaseService, IsDeviceOfflineSyncRequired);

                startTheConfigurationUpdate(_databaseService);

                _logger.LogInformation("SystemsManagerAgent started successfully");

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in SystemsManagerAgent.start()");
            }
        }

        private void startTheConfigurationUpdate(DatabaseService _databaseService)
        {
            _logger.LogInformation("startTheConfigurationUpdate called");
            ConfigUpdateSubscriber subs = new ConfigUpdateSubscriber(_configProperties, _loggerFactory, _databaseService);
            subs.ConnectAsync().GetAwaiter().GetResult();
        }

        private void startTheDeviceOfflineSyncEnabled(DatabaseService _databaseService, string IsDeviceOfflineSyncRequired)
        {
            try
            {
                _logger.LogInformation("IsDeviceOfflineSyncRequired - {IsDeviceOfflineSyncRequired}", IsDeviceOfflineSyncRequired);
                if (IsDeviceOfflineSyncRequired.Equals("Y"))
                {
                    DeviceOfflineDataSyncSchduler syncSchduler = new(_configProperties, _loggerFactory, _databaseService);
                    syncSchduler.StartScheduler();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception in starting the startTheDeviceOfflineSyncEnabled in SystemsManagerAgent");
            }
        }

        private void captureTheOfflineStatus(DatabaseService _databaseService, string deviceOfflineStatusCheckEnabled)
        {
            try
            {
                _logger.LogInformation("deviceOfflineStatusCheckEnabled - {deviceOfflineStatusCheckEnabled}", deviceOfflineStatusCheckEnabled);
                if (deviceOfflineStatusCheckEnabled.Equals("Y"))
                {
                    DeviceOnlineStatusCheckScheduler deviceOfflineDataSyncSchduler = new(_configProperties, _databaseService, _loggerFactory);
                    deviceOfflineDataSyncSchduler.StartScheduler();
                }
                if (deviceOfflineStatusCheckEnabled.Equals("N"))
                {
                    // database_file_path -> SM_SQLLITE_FILE_PATH
                    string _localDBPath = _configProperties.GetPropertyValue("SM_SQLLITE_FILE_PATH");
                    DeviceConnectivityStatus _deviceConnectivityStatus = _databaseService.GetDeviceConnectivityStatusById(1);
                    _deviceConnectivityStatus.status = "connected";
                    _databaseService.UpdateDeviceConnectivityStatus(_deviceConnectivityStatus);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception in starting the captureTheOfflineStatus in SystemsManagerAgent");
            }
        }

        private void captureTheSensorHeartBeats(DatabaseService _databaseService, string IsSensorHeartBeatRequired)
        {
            try
            {
                _logger.LogInformation("IsSensorHeartBeatRequired - {IsSensorHeartBeatRequired}", IsSensorHeartBeatRequired);
                if (IsSensorHeartBeatRequired.Equals("Y"))
                {
                    SensorHeartBeatCloudCommunicationManager cloudCommMgr = new(_configProperties, _loggerFactory);
                    SensorHeartBeatSubscriber heartBeatSubscriber = new(_configProperties, _loggerFactory, cloudCommMgr, _databaseService);
                    heartBeatSubscriber.ConnectAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception in starting the captureTheSensorHeartBeats in SystemsManagerAgent");
            }
        }

        private void captureTheServiceMetrics(DatabaseService _databaseService, string IsServiceMetricsRequired)
        {
            try
            {
                _logger.LogInformation("IsServiceMetricsRequired - {IsServiceMetricsRequired}", IsServiceMetricsRequired);
                if (IsServiceMetricsRequired.Equals("Y"))
                {
                    ServiceMetricsCloudCommunicationManager cloudCommunicationMgrService = new(_configProperties, _loggerFactory, _databaseService);
                    K3sMetricsService metricService = new(_configProperties, _loggerFactory, _databaseService);
                    ServiceMetricsScheduler serviceMetricsSchduler = new(_configProperties, cloudCommunicationMgrService, metricService, _loggerFactory, _databaseService);
                    serviceMetricsSchduler.StartScheduler();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception in starting the captureTheServiceMetrics in SystemsManagerAgent");
            }
        }

        private void captureTheSystemMetrics(DatabaseService _databaseService, string IsSystemMetricsRequired)
        {
            try
            {
                _logger.LogInformation("IsSystemMetricsRequired - {IsSystemMetricsRequired}", IsSystemMetricsRequired);
                if (IsSystemMetricsRequired.Equals("Y"))
                {
                    DeviceMetricsCloudCommunicationManager cloudCommunicationMgrDevice = new(_configProperties, _loggerFactory, _databaseService);
                    K3sMetricsService metricService = new(_configProperties, _loggerFactory, _databaseService);
                    SystemMetricsScheduler systemMetricsSchduler = new(_configProperties, cloudCommunicationMgrDevice, metricService, _loggerFactory, _databaseService);
                    systemMetricsSchduler.StartScheduler();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "exception in starting the captureTheSystemMetrics in SystemsManagerAgent");
            }
        }
    }
}