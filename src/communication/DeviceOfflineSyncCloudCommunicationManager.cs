using Authlete.Dto;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

namespace systems_manager.src.communication
{
    public class DeviceOfflineSyncCloudCommunicationManager
    {
        private string brokerAddress;
        private string brokerPort;
        private ConfigPropertiesReader _configPropertiesReader;
        private string localDBPath;
        private string clientIdDynamic;
        private IMqttClient mqttClient;
        private string keepAliveTime;
        private MqttFactory mqttFactory;
        private readonly ILogger<DeviceOfflineSyncCloudCommunicationManager> _logger;
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);

        public DeviceOfflineSyncCloudCommunicationManager(ConfigPropertiesReader _configPropertiesReader, ILoggerFactory _loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<DeviceOfflineSyncCloudCommunicationManager>();
            this._configPropertiesReader = _configPropertiesReader;
            // cloud_configurations/mqtt_broker_address -> CLOUD_CARE_HOST
            brokerAddress = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
            // cloud_configurations/mqtt_broker_port -> MQTT_PORT
            brokerPort = _configPropertiesReader.GetPropertyValue("MQTT_PORT");
            this.mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            // application/mqtt_keep_alive_time -> SM_APP_MQTT_KEEP_ALIVE_TIME
            this.keepAliveTime = _configPropertiesReader.GetPropertyValue("SM_APP_MQTT_KEEP_ALIVE_TIME");
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
                _logger.LogDebug("DeviceOfflineSyncCloudCommunicationManager details - clientIdStr: {clientId}", clientId);
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
                _logger.LogInformation("DeviceOfflineSyncCloudCommunicationManager details - Topic: {Topic}, BrokerAddress: {BrokerAddress}, BrokerPort: {BrokerPort}, ClientId: {ClientIdDynamic}", topic, brokerAddress, brokerPort, clientId);
                if (!mqttClient.IsConnected)
                {
                    await EnsureConnectedAsync(clientId);
                }

                var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(messageObj)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

                await mqttClient.PublishAsync(message);

                _logger.LogInformation("DeviceOfflineSyncCloudCommunicationManager Message Published - {topic}", topic);
                // Log success information...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceOfflineSyncCloudCommunicationManager Exception Details: ");
                throw;
            }
        }
    }
}