using ServiceManager;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using systems_manager.src.agent;
using systems_manager.src.communication;
using systems_manager.src.service;

namespace systems_manager.src.schdulers
{
    public class DeviceOnlineStatusCheckScheduler : IDisposable
    {
        private Timer _timer;
        private readonly ConfigPropertiesReader _configPropertiesReader;
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;
        private readonly InternetConnectionChecker _internetConnectionChecker = new();

        private readonly string cloudCareIP;
        private readonly string assetManagerPort;
        private readonly string ipdaressPingForOnlineStatus;

        public DeviceOnlineStatusCheckScheduler(
            ConfigPropertiesReader configPropertiesReader,
            DatabaseService databaseService,
            ILoggerFactory loggerFactory)
        {
            _configPropertiesReader = configPropertiesReader;
            _databaseService = databaseService;
            _logger = loggerFactory.CreateLogger<DeviceOnlineStatusCheckScheduler>();

            cloudCareIP = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
            assetManagerPort = _configPropertiesReader.GetPropertyValue("ASSET_MANAGER_PORT");
            ipdaressPingForOnlineStatus = "https://" + cloudCareIP  + "/" + _configPropertiesReader.GetPropertyValue("SM_DEVICE_STATUS_CHECK_URL");
        }

        public void StartScheduler()
        {
            // application/device_online_status_check_frequency -> SM_APP_DEVICE_ONLINE_STATUS_CHECK_FREQUENCY
            string frequencyProperty = _configPropertiesReader.GetPropertyValue("SM_APP_DEVICE_ONLINE_STATUS_CHECK_FREQUENCY");
            _logger.LogDebug("frequencyProperty - {FrequencyProperty}", frequencyProperty);
            if (int.TryParse(frequencyProperty, out int frequencySeconds))
            {
                _logger.LogInformation("Scheduler frequency set to every {FrequencySeconds} seconds.", frequencySeconds);
                _timer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(frequencySeconds));
            }
            else
            {
                _logger.LogError("Invalid frequency value in the properties file: {FrequencyProperty}", frequencyProperty);
                throw new ArgumentException("Invalid frequency value in the properties file");
            }
        }

        private void CollectMetrics(object state)
        {
            IsInternetAvailable();
        }

        private void IsInternetAvailable()
        {
            bool status = _internetConnectionChecker.IsInternetConnectedAsync(ipdaressPingForOnlineStatus).GetAwaiter().GetResult();
            DeviceConnectivityStatus deviceConnectivityStatus = _databaseService.GetDeviceConnectivityStatusById(1);
            _logger.LogInformation("Internet Connected - {status}", status);
            if (status)
            {
                if (deviceConnectivityStatus.status != "connected")
                {
                    deviceConnectivityStatus.status = "connected";
                    deviceConnectivityStatus.timestamp = DateTime.Now;
                    _databaseService.UpdateDeviceConnectivityStatus(deviceConnectivityStatus);
                }
            }
            else
            {
                if (deviceConnectivityStatus.status != "disconnected")
                {
                    deviceConnectivityStatus.status = "disconnected";
                    deviceConnectivityStatus.timestamp = DateTime.Now;
                    _databaseService.UpdateDeviceConnectivityStatus(deviceConnectivityStatus);
                }
            }
        }

        public void StopScheduler()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}