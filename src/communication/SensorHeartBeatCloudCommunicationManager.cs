using MQTTnet;
using MQTTnet.Client;

namespace systems_manager.src.communication
{
    public class SensorHeartBeatCloudCommunicationManager
    {
        private string brokerAddress;
        private string brokerPort;
        private ConfigPropertiesReader _configPropertiesReader;
        private string localDBPath;
        private string clientIdDynamic;
        private IMqttClient mqttClient;
        private string keepAliveTime;
        private MqttFactory mqttFactory;
        private readonly ILogger<SensorHeartBeatCloudCommunicationManager> _logger;
        private readonly SemaphoreSlim connectLock = new SemaphoreSlim(1, 1);

        public SensorHeartBeatCloudCommunicationManager(ConfigPropertiesReader _configPropertiesReader, ILoggerFactory _loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<SensorHeartBeatCloudCommunicationManager>();
            this._configPropertiesReader = _configPropertiesReader;
            // cloud_configurations/mqtt_broker_address -> CLOUD_CARE_HOST
            brokerAddress = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
            // cloud_configurations/mqtt_broker_port -> MQTT_PORT
            brokerPort = _configPropertiesReader.GetPropertyValue("MQTT_PORT");
            this.mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            // application/mqtt_keep_alive_time -> SM_APP_MQTT_KEEP_ALIVE_TIME
            this.keepAliveTime = _configPropertiesReader.GetPropertyValue("SM_APP_MQTT_KEEP_ALIVE_TIME");
            // heart_beat/cloud_brocker_client_id -> SM_HEART_BEAT_CLOUD_BROCKER_CLIENT_ID
            string deviceId = _configPropertiesReader.GetPropertyValue("DEVICE_ID");
            string cloudClientId = "SM_HEARTBEAT_CLOUD_CLIENT_"+deviceId;
            //string cloudClientId = _configPropertiesReader.GetPropertyValue("SM_HEART_BEAT_CLOUD_BROCKER_CLIENT_ID");
            string clientIdString = $"{cloudClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
            EnsureConnectedAsync(clientIdString).GetAwaiter().GetResult();
        }


        public async Task EnsureConnectedAsync(string clientId)
        {
            await connectLock.WaitAsync();
            try
            {
                string clientIdStr = Guid.NewGuid().ToString();
                string clientIdStrnig = clientId + "_" + clientIdStr;
                var options = new MqttClientOptionsBuilder()
                    .WithClientId(clientIdStrnig)
                    .WithTcpServer(brokerAddress, int.Parse(brokerPort))
                    .WithCleanSession(true)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(int.Parse(keepAliveTime)))
                .Build();
                _logger.LogDebug("SensorHeartBeatCloudCommunicationManager details - clientIdStr: {clientIdStrnig}", clientIdStrnig);
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
                _logger.LogInformation("SensorHeartBeatCloudCommunicationManager details - Topic: {Topic}, BrokerAddress: {BrokerAddress}, BrokerPort: {BrokerPort}, ClientId: {ClientIdDynamic}", topic, brokerAddress, brokerPort, clientId);
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
                _logger.LogInformation("SensorHeartBeatCloudCommunicationManager Message Published - {topic}", topic);
                // Log success information...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " SensorHeartBeatCloudCommunicationManager Exception Details: ");
                throw;
            }
        }
    }
}