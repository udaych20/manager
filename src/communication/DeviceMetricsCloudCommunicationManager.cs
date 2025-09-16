using Authlete.Dto;
using MQTTnet;
using MQTTnet.Client;
using systems_manager.src.service;

namespace systems_manager.src.communication
{
    public class DeviceMetricsCloudCommunicationManager
    {
        private string brokerAddress;
        private string brokerPort;
        private ConfigPropertiesReader _configPropertiesReader;
        private string localDBPath;
        private string clientIdDynamic;
        private IMqttClient mqttClient;
        private string keepAliveTime;
        private MqttFactory mqttFactory;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<DeviceMetricsCloudCommunicationManager> _logger;
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);

        public DeviceMetricsCloudCommunicationManager(ConfigPropertiesReader _configPropertiesReader,
            ILoggerFactory _loggerFactory,
            DatabaseService _databaseService)
        {
            this._databaseService = _databaseService;
            _logger = _loggerFactory.CreateLogger<DeviceMetricsCloudCommunicationManager>();
            this._configPropertiesReader = _configPropertiesReader;
            // cloud_configurations/mqtt_broker_address -> CLOUD_CARE_HOST
            brokerAddress = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
            // cloud_configurations/mqtt_broker_port -> MQTT_PORT
            brokerPort = _configPropertiesReader.GetPropertyValue("MQTT_PORT");
            this.mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            // application/mqtt_keep_alive_time -> SM_APP_MQTT_KEEP_ALIVE_TIME
            this.keepAliveTime = _configPropertiesReader.GetPropertyValue("SM_APP_MQTT_KEEP_ALIVE_TIME");
            //string clientId = _configPropertiesReader.GetPropertyValue("SM_DEVICE_METRICS_PUBLISH_CLIENT_ID");
            string deviceId = _configPropertiesReader.GetPropertyValue("DEVICE_ID");
            string clientId = "SM_DEV_MET_P_CLIENT_" + deviceId;
            string clientIdString = $"{clientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
            EnsureConnectedAsync(clientIdString).GetAwaiter().GetResult();
        }

        public async Task EnsureConnectedAsync(string clientId)
        {
            await connectLock.WaitAsync();
            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(clientId)
                    .WithTcpServer(brokerAddress, int.Parse(brokerPort))
                    .WithCleanSession(true)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(int.Parse(keepAliveTime)))
                .Build();
                await mqttClient.ConnectAsync(options);
            }
            catch
            {
                throw;
            }
            finally
            {
                connectLock.Release();
            }
        }

        public async void PublishMessage(string topic, string clientId, string messageObj)
        {
            try
            {
                _logger.LogInformation("DeviceMetricsCloudCommunicationManager details - Topic: {Topic}, BrokerAddress: {BrokerAddress}, BrokerPort: {BrokerPort}, clientId: {clientId}", topic, brokerAddress, brokerPort, clientId);
                //if (!mqttClient.IsConnected)
                //{
                //    await EnsureConnectedAsync(clientId);
                //}
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(messageObj)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await mqttClient.PublishAsync(message);
                _logger.LogInformation("DeviceMetricsCloudCommunicationManager Message Published - {topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " DeviceMetricsCloudCommunicationManager Exception Details: ");
                throw;
            }
        }
    }
}