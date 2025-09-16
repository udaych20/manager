
using systems_manager.src.service;

namespace systems_manager.endpoints
{
    public class ServiceEndpoints(WebApplication app, DatabaseService databaseService, AlertService alertService, EventService eventService, ILoggerFactory loggerFactory)
    {

        private static ILogger _logger;

        public void Init()
        {
            _logger = loggerFactory.CreateLogger<ServiceEndpoints>();
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
            }

            app.MapPost("/services", (Service service) =>
            {
                databaseService.CreateService(service);
                return Results.Created($"/services/{service.Id}", service);
            });

            app.MapGet("/services/{serviceName}", (string serviceName) =>
            {
                var service = databaseService.GetServiceByServieName(serviceName);
                return service != null ? Results.Ok(service) : Results.NotFound();
            });

            app.MapGet("/services", () =>
            {
                return Results.Ok(databaseService.GetAllServices());
            });

            app.MapPut("/services/{id}", (int id, Service updatedService) =>
            {
                var service = databaseService.GetServiceById(id); // Assuming you have a GetServiceById method
                if (service == null) return Results.NotFound();

                // Update properties
                service.ServiceName = updatedService.ServiceName;
                service.Category = updatedService.Category;
                service.Priority = updatedService.Priority;
                service.Status = updatedService.Status;
                service.Mode = updatedService.Mode;

                databaseService.UpdateService(service);
                return Results.Ok(service);
            });

            app.MapDelete("/services/{id}", (int id) =>
            {
                databaseService.DeleteService(id);
                return Results.Ok();
            });


            app.MapGet("/service-metrics/getServiceMetricsByDuration/{duration}", (int duration) =>
            {
                return databaseService.GetNodeServiceMetricsByMinutes(duration);
            });

            app.MapGet("/service-metrics/getAllServiceMetrics", () =>
            {
                return databaseService.GetAllNodeServiceMetrics();
            });

            app.MapGet("/node-metrics/getAllNodeMetrics", () =>
            {
                return databaseService.GetAllNodeMetrics();
            });


            app.MapGet("/metrics/deleteAllMetrics", () =>
            {
                databaseService.DeleteAllDeviceMetrics();
                databaseService.DeleteAllServiceMetrics();
                return Results.Ok();
            });



            app.MapGet("/node-metrics/getNodeMetricsByDuration/{duration}", (int duration) =>
            {
                return databaseService.GetNodeMetricsByMinutes(duration);
            });

            app.MapGet("/event-log/getAllEventLogs", () =>
            {
                return databaseService.GetAllEventLogs();
            });
            app.MapGet("/event-log/getAllEventLogsByServiceName/{serviceName}", (string serviceName) =>
            {
                return databaseService.GetAllEventLogsByServiceName(serviceName);
            });

            app.MapGet("/event-log/getAllEventLogsByServiceNameAndPolicyName/{serviceName}/{policyName}", (string serviceName,string policyName) =>
            {
                return databaseService.GetAllEventLogsByServiceNameAndPolicyName(serviceName, policyName);
            });

            app.MapGet("/event-log/GetAllEventLogsByPolicyNameAndActionTaken/{policyName}/{actionTaken}", (string policyName, string actionTaken) =>
            {
                return databaseService.GetAllEventLogsByPolicyNameAndActionTaken(policyName, actionTaken);
            });


            app.MapGet("/event-log/getAllEventLogsByPolicyName/{policyName}", (string policyName) =>
            {
                return databaseService.GetAllEventLogsByPolicyName(policyName);
            });

            app.MapPost("/event-log/add", async (EventLog logEntry) =>
            {
                try
                {
                    if (logEntry == null)
                    {
                        return Results.BadRequest("Invalid event log data.");
                    }

                    await eventService.addEventLogAsync(logEntry);
                    return Results.Ok(new { message = "EventLog processed successfully.", logId = logEntry.LogId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing EventLog: {Message}", ex.Message);
                    return Results.Problem(ex.Message);
                }
            });

            app.MapPut("/event-log/update", async (EventLog updatedLog) =>
            {
                try
                {
                    var existingLog = await databaseService.GetEventLogByIdAsync(updatedLog.LogId);
                    if (existingLog == null)
                    {
                        return Results.NotFound(new { message = $"EventLog with LogId {updatedLog.LogId} not found." });
                    }

                    await databaseService.UpdateEventLogAsync(updatedLog);
                    return Results.Ok(new { message = "EventLog updated successfully.", logId = updatedLog.LogId });
                }
                catch (Exception ex)
                {
                    // Log the exception details
                    return Results.Problem(ex.Message);
                }
            });

            app.MapPost("/alert/add", async (AlertDto alertDto) =>
            {
                try
                {
                    if (alertDto == null)
                    {
                        return Results.BadRequest("Invalid alert data.");
                    }

                    bool result = await alertService.RaiseAlertAsync(
                        alertDto.Value,
                        alertDto.Description,
                        alertDto.DestinationName,
                        alertDto.DestinationType,
                        alertDto.ErrorDetails,
                        alertDto.RaisedBy);

                    if (result)
                    {
                        return Results.Ok(new { message = "Alert raised successfully." });
                    }
                    else
                    {
                        return Results.Problem("Failed to raise alert.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error raising alert: {Message}", ex.Message);
                    return Results.Problem(ex.Message);
                }
            });

        }
    }
}