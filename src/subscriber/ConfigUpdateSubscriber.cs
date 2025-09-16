using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using systems_manager.src.communication;
using Newtonsoft.Json.Linq;
using systems_manager.src.service;
using System.Reactive;
using k8s;
using Authlete.Dto;

public class ConfigUpdateSubscriber
{
    private IMqttClient _client;

    private string brockerAddress;

    private long deviceId;

    private string configUpdatePort;

    private string configUpdateTopic;

    private string configUpdateclientId;

    private string cloudClientId;

    private readonly string keepAliveTime;

    private DatabaseService _databaseService;

    private readonly ILogger<ConfigUpdateSubscriber> _logger;

    private SensorHeartBeatCloudCommunicationManager cloudCommMgr;

    private Kubernetes kubernetesClient;

    private string k3sNamespace;

    private KubernetesDeploymentUpdater deploymentUpdate;

    public ConfigUpdateSubscriber(
        ConfigPropertiesReader _configPropertiesReader,
        ILoggerFactory _loggerFactory,
        DatabaseService _databaseService)
    {
        _logger = _loggerFactory.CreateLogger<ConfigUpdateSubscriber>();
        _logger.LogInformation("ConfigUpdateSubscriber obj created");
        // application/k3s_namespace -> SM_APP_K3S_NAMESPACE
        this.k3sNamespace = _configPropertiesReader.GetPropertyValue("SM_APP_K3S_NAMESPACE");
        // application/deviceId -> DEVICE_ID
        this.deviceId = long.Parse(_configPropertiesReader.GetPropertyValue("DEVICE_ID"));
        // cloud_configurations/mqtt_broker_address -> CLOUD_CARE_HOST
        this.brockerAddress = _configPropertiesReader.GetPropertyValue("CLOUD_CARE_HOST");
        // cloud_configurations/mqtt_broker_port -> MQTT_PORT
        this.configUpdatePort = _configPropertiesReader.GetPropertyValue("MQTT_PORT");
        // application/mqtt_keep_alive_time -> SM_APP_MQTT_KEEP_ALIVE_TIME
        this.keepAliveTime = _configPropertiesReader.GetPropertyValue("SM_APP_MQTT_KEEP_ALIVE_TIME");
        // cloud_configurations/config_update_topic -> SM_CONFIG_CONFIG_UPDATE_SUBSCRIBE_TOPIC
        this.configUpdateTopic = deviceId + "/" + _configPropertiesReader.GetPropertyValue("SM_CONFIG_CONFIG_UPDATE_SUBSCRIBE_TOPIC");
        //this.configUpdateclientId = _configPropertiesReader.GetPropertyValue("SM_CONFIG_CONFIG_UPDATE_SUBSCRIBE_CLIENT_ID");
        this.configUpdateclientId = "SM_CONF_UPD_CLIENT_" + deviceId;
        this._databaseService = _databaseService;
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        // application/enable_local_testing -> SM_APP_ENABLE_LOCAL_TESTING
        string localTestFlag = _configPropertiesReader.GetPropertyValue("SM_APP_ENABLE_LOCAL_TESTING");
        KubernetesClientConfiguration config;
        if (!localTestFlag.Equals("Y"))
        {
            config = KubernetesClientConfiguration.InClusterConfig();
        }
        else
        {
            config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        }
        kubernetesClient = new Kubernetes(config);
        deploymentUpdate = new(kubernetesClient, _loggerFactory);
    }

    public async Task ConnectAsync()
    {
        _logger.LogInformation("ConfigUpdateSubscriber.ConnectAsync() called");
        string deviceClientlientIdString = $"{cloudClientId}-{(long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds}";
        var _options = new MqttClientOptionsBuilder()
            .WithTcpServer(brockerAddress, int.Parse(configUpdatePort))
            .WithCleanSession(true)
            .WithClientId(deviceClientlientIdString)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(int.Parse(keepAliveTime)))
            .Build();
        await _client.ConnectAsync(_options);
        _logger.LogInformation("ConfigUpdateSubscriber.ConnectAsync() end method");
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
    {
        _logger.LogInformation("ConfigUpdateSubscriber Connected to mqtt client {brockerAddress}", brockerAddress);
        await SubscribeToTopicAsync(this.configUpdateTopic);
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        _logger.LogInformation("ConfigUpdateSubscriber Disconnected to mqtt client {brockerAddress}", brockerAddress);
        return Task.CompletedTask;
    }

    [Obsolete]
    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
        _logger.LogInformation("Message received: {payload}", payload);
        try
        {
            StandardMessage message = JsonConvert.DeserializeObject<StandardMessage>(payload);
            ConfigMessage configMessage = JsonConvert.DeserializeObject<ConfigMessage>(message.Message);
            if (configMessage.DeviceId != deviceId)
            {
                _logger.LogInformation("config update ignored because its divice mismatch");
                return Task.CompletedTask;
            }
            configMessage.Config.ForEach(x =>
            {
                //string key = x.GetValueOrDefault("key");
                string value = x.GetValueOrDefault("category");
                ConfigurationDetails config = _databaseService.GetConfigurationDetailsByKey("category");
                DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).DateTime;
                if (config == null)
                {
                    // If the configuration for the key doesn't exist, insert a new one.
                    ConfigurationDetails newConfig = new ConfigurationDetails
                    {
                        Key = "category",
                        Value = value,
                        Timestamp = dateTime.ToLocalTime(),
                        Source = message.Source,
                        serviceName = configMessage.ServiceName,
                    };
                    _databaseService.InsertConfigurationDetails(newConfig);
                    _logger.LogInformation("Inserted new configuration for key: category : {value}", value);
                }
                else
                {
                    // If the configuration exists, update it with the new value.
                    config.Value = value;
                    config.Timestamp = dateTime.ToLocalTime(); // Update the timestamp to the current time
                    _databaseService.UpdateConfigurationDetails(config);
                    //deviceService.RestartPodsAsync(deviceId, configMessage.ServiceId).GetAwaiter().GetResult();
                    _logger.LogInformation("Updated configuration for key: category , value: {value}", value);
                }
                deploymentUpdate.UpdateDeploymentLabel(configMessage.ServiceName + "-deployment", k3sNamespace, "workloadType", value);
                _logger.LogInformation("workloadType updated for servuce {configMessage.ServiceName}", configMessage.ServiceName);
                UpdateServiceIntoServiceTable(_databaseService, configMessage);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing configuration message");
        }

        return Task.CompletedTask;
    }

    private async Task SubscribeToTopicAsync(string topic)
    {
        _logger.LogInformation("ConfigUpdateSubscriber.SubscribeToTopicAsync() start method");
        if (_client.IsConnected)
        {
            await _client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            _logger.LogInformation("Subscribed to topic {topic}", topic);
        }
        else
        {
            _logger.LogInformation("ConfigUpdateSubscriber.SubscribeToTopicAsync() client not connected");
        }
    }

    private static void UpdateServiceIntoServiceTable(DatabaseService _databaseService, ConfigMessage configMessage)
    {
        string catogery = "";
        foreach (var catogeryMap in configMessage.Config)
        {
            if (catogeryMap.TryGetValue("category", out string category))
            {
                catogery = catogeryMap["category"];
            }
        }
        Service service = _databaseService.GetServiceByServieName(configMessage.ServiceName);
        if (service == null)
        {
            var newService = new Service
            {
                ServiceName = configMessage.ServiceName,
                Mode = "device",
                Priority = 50,
                Status = "active",
                Category = catogery
            };
            // TODO: remove later
            if (!newService.ServiceName.Equals("health-score-service"))
            {
                newService.Category = "critical";
            }
            _databaseService.CreateService(newService);
        }
        else
        {
            service.Category = catogery;
            _databaseService.UpdateService(service);
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