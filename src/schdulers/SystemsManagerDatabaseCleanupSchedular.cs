using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Framework.Common;
using ServiceManager;
using System.Text.Json;
using System.Text.Json.Serialization;
using systems_manager.src.communication;
using systems_manager.src.service;

namespace systems_manager.src.schdulers
{
    public class SystemsManagerDatabaseCleanupSchedular
    {
        private readonly ILogger<SystemsManagerDatabaseCleanupSchedular> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private Timer _timer;
        private readonly string _localDBPath;
        private readonly ConfigPropertiesReader _configPropertiesReader;
        private readonly DatabaseService _databaseService;


        public SystemsManagerDatabaseCleanupSchedular(ConfigPropertiesReader _configPropertiesReader,
            ILoggerFactory _loggerFactory,
            DatabaseService _databaseService
            )
        {
            _logger = _loggerFactory.CreateLogger<SystemsManagerDatabaseCleanupSchedular>();
            this._loggerFactory = _loggerFactory;
            this._databaseService = _databaseService;
            this._configPropertiesReader = _configPropertiesReader;
        }

        public void StartScheduler()
        {
            // application/database_cleanup_schedular_frequency_in_days -> SM_DB_CLEAN_SCHDLR_FREQ_IN_DAYS
            string frequencyProperty = _configPropertiesReader.GetPropertyValue("SM_DB_CLEAN_SCHDLR_FREQ_IN_DAYS");
            _logger.LogInformation("ScaledownPolicySchdular.StartScheduler()  frequencyProperty - {frequencyProperty} days", frequencyProperty);
            if (int.TryParse(frequencyProperty, out int frequencySeconds))
            {
                _timer = new Timer(Clean, null, TimeSpan.Zero, TimeSpan.FromDays(frequencySeconds));
            }
            else
            {
                throw new ArgumentException("Invalid frequency value in the properties file");
            }
        }

        private void Clean(object state)
        {
            _databaseService.DeleteAllDeviceMetrics();
            _databaseService.DeleteLog();
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
}