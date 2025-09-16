using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using systems_manager.src.service;

public class EventService
{
    private readonly DatabaseService _databaseService;
    private readonly AlertService _alertService;
    private readonly CloudService _cloudService;
    private readonly ILogger<EventService> _logger;

    public EventService(DatabaseService databaseService, AlertService alertService, CloudService cloudService, ILoggerFactory loggerFactory)
    {
        _databaseService = databaseService;
        _alertService = alertService;
        _cloudService = cloudService;
        _logger = loggerFactory.CreateLogger<EventService>();
    }

    public async Task addEventLogAsync(EventLog logEntry)
    {
        string status = DetermineStatus(logEntry);
        _logger.LogInformation($"addEventLogAsync - status-{status}, {logEntry}", status, logEntry);

        await _databaseService.InsertEventLogAsync(logEntry);
        await _cloudService.UpdateServiceStatusAsync(logEntry.ServiceName, status, logEntry.DestinationNodeType, logEntry.ActionTaken);
        await _alertService.RaiseAlertAsync(
            logEntry.Details,
            logEntry.ActionTaken,
            logEntry.ServiceName,
            "SERVICE",
            logEntry.Details,
            "SystemsManager"
        );
    }

    private string DetermineStatus(EventLog logEntry)
    {
        var actionMap = logEntry.IsNotification ? EventActionMappings.NotificationActions : EventActionMappings.DefaultActions;
        actionMap.TryGetValue(logEntry.ActionTaken, out string actionString);
        return actionString ?? "";
    }

}
