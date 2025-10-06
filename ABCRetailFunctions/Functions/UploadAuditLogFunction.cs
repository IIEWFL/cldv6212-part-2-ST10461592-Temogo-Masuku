using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetailFunctions.Functions
{
    public class UploadAuditLogFunction
    {
        private readonly ILogger<UploadAuditLogFunction> _logger;
        private readonly QueueStorageService _queueStorageService;

        public UploadAuditLogFunction(ILogger<UploadAuditLogFunction> logger, QueueStorageService queueStorageService)
        {
            _logger = logger;
            _queueStorageService = queueStorageService;
        }

        [Function("UploadAuditLog")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "queue/auditlog")] HttpRequest req)
        {
            _logger.LogInformation("Uploading audit log to queue");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody);

                if (logData == null)
                {
                    return new BadRequestObjectResult("Log data is required");
                }

                var logMessage = new
                {
                    Timestamp = DateTime.UtcNow,
                    Action = logData.GetValueOrDefault("Action", "Unknown"),
                    EntityType = logData.GetValueOrDefault("EntityType", "Unknown"),
                    Details = logData.GetValueOrDefault("Details", new { })
                };

                await _queueStorageService.SendLogEntryAsync(logMessage);

                _logger.LogInformation("Audit log uploaded successfully");
                return new OkObjectResult(new { message = "Audit log uploaded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading audit log: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}