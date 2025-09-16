using systems_manager.src.agent;
using SQLitePCL;
using systems_manager.src.service;
using systems_manager.endpoints;

namespace systems_manager.src
{
    public class SystemsManager
    {
        private static ILogger _logger;
        public static void Initialize(ILoggerFactory loggerFactory, WebApplication app, ConfigPropertiesReader configPropertiesReader)
        {

            _logger = loggerFactory.CreateLogger(typeof(SystemsManager));
            try
            {
                Batteries.Init();
                // app-name -> SM_APP_APP_NAME
                _logger.LogInformation("{AppName} Application Started ...", configPropertiesReader.GetPropertyValue("SM_APP_APP_NAME"));
                // database_file_path -> SM_SQLLITE_FILE_PATH
                DatabaseService databaseService = new(configPropertiesReader.GetPropertyValue("SM_SQLLITE_FILE_PATH"), loggerFactory);
                // enable_system_service_metrics_agent -> SM_APP_ENABLE_SYSTEM_SERVICE_METRICS_AGENT
                string EnableSystemServiceMetricsFeature = configPropertiesReader.GetPropertyValue("SM_APP_ENABLE_SYSTEM_SERVICE_METRICS_AGENT");
                _logger.LogInformation("EnableSystemServiceMetricsFeature - {EnableSystemServiceMetricsFeature}", EnableSystemServiceMetricsFeature);
                if ("Y".Equals(EnableSystemServiceMetricsFeature))
                {
                    SystemsManagerAgent agent = new(configPropertiesReader, loggerFactory, databaseService);
                    Task.Run(() => agent.Start());
                }
                HttpClient client = new HttpClient();
                AlertService alertService = new AlertService(configPropertiesReader, loggerFactory);
                CloudService cloudService = new CloudService(configPropertiesReader, loggerFactory);
                EventService eventService = new EventService(databaseService, alertService, cloudService, loggerFactory);
                ServiceEndpoints serviceEndpoints = new(app, databaseService, alertService, eventService, loggerFactory);
                serviceEndpoints.Init();

                // alert created for local test
                //AlertService alertService = new AlertService(configPropertiesReader);
                //alertService.RaiseAlertAsync(null, "health-score-service moving to cloud as device resources are high", "health-score-service", "service", "health-score-service moving to cloud as device resources are high", "systems-manager").GetAwaiter();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in SystemsManager.Initialize()");
            }
        }
    }
}