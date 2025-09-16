using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using systems_manager.src.communication;
using Newtonsoft.Json.Linq;
using systems_manager.src.service;

public class SensorHeartBeatSubscriber
{
    private IMqttClient _client;

    private string brockerAddress;

    private long deviceId;

    private string port;

    private string topic;

    private string clientId;

    private string cloudClientId;
    private readonly string keepAliveTime;
    private DatabaseService _databaseService;
    private readonly ILogger<SensorHeartBeatSubscriber> _logger;

    private SensorHeartBeatCloudCommunicationManager cloudCommMgr;

    private string cloudTopic;

    public SensorHeartBeatSubscriber(
        ConfigPropertiesReader _configPropertiesReader,
        ILoggerFactory _loggerFactory,
        SensorHeartBeatCloudCommunicationManager cloudCommMgr,
        DatabaseService _databaseService)
    {
        this.cloudCommMgr = cloudCommMgr;
        _logger = _loggerFactory.CreateLogger<SensorHeartBeatSubscriber>();
        _logger.LogInformation("SensorHeartBeatSubscriber obj created");
        this.brockerAddress = _configPropertiesReader.GetPropertyValue("DEVICE_HOST");
        this.port = _configPropertiesReader.GetPropertyValue("MQTT_PORT");
        this.topic = _configPropertiesReader.GetPropertyValue("SM_DEVICE_HEARTBEAT_MSGS_SUBSCRIBE_TOPIC");
        // application/deviceId -> DEVICE_ID
        this.deviceId = long.Parse(_configPropertiesReader.GetPropertyValue("DEVICE_ID"));
        clientId = "SM_HEART_BEAT_CLIENT_" + this.deviceId;
        this.cloudTopic = _configPropertiesReader.GetPropertyValue("SM_HEART_BEAT_CLOUD_PUBLISH_TOPIC");
        this.cloudClientId = "sm_d_hb_cloud_" + this.deviceId + "_client";
        this.keepAliveTime = _configPropertiesReader.GetPropertyValue("SM_APP_MQTT_KEEP_ALIVE_TIME");
        this._databaseService = _databaseService;
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task ConnectAsync()
    {
        _logger.LogInformation("SensorHeartBeatSubscriber.ConnectAsync() called");
        string deviceClientlientIdString = $"{cloudClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
        var _options = new MqttClientOptionsBuilder()
            .WithTcpServer(brockerAddress, int.Parse(port))
            .WithCleanSession(true)
            .WithClientId(deviceClientlientIdString)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(int.Parse(keepAliveTime))) // 30 secs
            .Build();
        await _client.ConnectAsync(_options);
        _logger.LogInformation("SensorHeartBeatSubscriber.ConnectAsync() end method");
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        _logger.LogInformation("SensorHeartBeatSubscriber Connected to mqtt client {brockerAddress}", brockerAddress);
        await SubscribeToTopicAsync(this.topic);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        _logger.LogInformation("SensorHeartBeatSubscriber Disconnected to mqtt client {brockerAddress}", brockerAddress);
        return Task.CompletedTask;
    }

    [Obsolete]
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
        string CloudClientlientIdString = $"{cloudClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
        _logger.LogInformation("Message received: {payload}", payload);
        try
        {
            var sensorData = JsonConvert.DeserializeObject<SensorHeartBeatData>(payload);
            JObject jsonObject = new();
            jsonObject["deviceId"] = deviceId;
            jsonObject["timestamp"] = sensorData.CurrentTimestamp.ToString();
            _logger.LogInformation("Timestamp: {sensorData.CurrentTimestamp}, Source: {sensorData.Source}", sensorData.CurrentTimestamp, sensorData.Source);
            if (sensorData.Source.Equals("device"))
            {
                jsonObject["isSensorConnected"] = "connected";
                _logger.LogInformation("healthipy is connected");
                cloudCommMgr.PublishMessage(this.cloudTopic, CloudClientlientIdString, jsonObject.ToString());

            }
            else if (sensorData.Source.Equals("simulated"))
            {
                jsonObject["isSensorConnected"] = "disconnected";
                _logger.LogInformation("healthipy is not connected");
                cloudCommMgr.PublishMessage(this.cloudTopic, CloudClientlientIdString, jsonObject.ToString());
            }
            DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
            var logInfo = new LogInformation
            {
                Topic = this.cloudTopic,
                InternetConnected = status.status,
                ClientId = CloudClientlientIdString,
                podName = status.RunningPodName,
                Message = jsonObject.ToString(),
                Timestamp = DateTime.UtcNow
            };
            _databaseService.InsertLog(logInfo).GetAwaiter();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing message: ");
            DeviceConnectivityStatus status = _databaseService.GetDeviceConnectivityStatusById(1);
            var logInfo = new LogInformation
            {
                Topic = this.cloudTopic,
                InternetConnected = status.status,
                ClientId = this.cloudClientId,
                podName = status.RunningPodName,
                Message = ex.ToString(),
                Timestamp = DateTime.UtcNow
            };
            _databaseService.InsertLog(logInfo).GetAwaiter();
        }

        return Task.CompletedTask;
    }

    private async Task SubscribeToTopicAsync(string topic)
    {
        _logger.LogInformation("SensorHeartBeatSubscriber.SubscribeToTopicAsync() start method");
        if (_client.IsConnected)
        {
            await _client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            _logger.LogInformation("Subscribed to topic {topic}", topic);
        }
        else
        {
            _logger.LogInformation("SensorHeartBeatSubscriber.SubscribeToTopicAsync() client not connected");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
    }
}