using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetMessagesFunction
    {
        private readonly ILogger<GetMessagesFunction> _logger;
        private readonly QueueStorageService _queueStorageService;

        public GetMessagesFunction(ILogger<GetMessagesFunction> logger, QueueStorageService queueStorageService)
        {
            _logger = logger;
            _queueStorageService = queueStorageService;
        }

        [Function("GetMessages")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queue/messages")] HttpRequest req)
        {
            _logger.LogInformation("Getting messages from queue");

            try
            {
                var auditLogs = await _queueStorageService.GetLogEntriesAsync();

                _logger.LogInformation($"Retrieved {auditLogs.Count} messages from queue");
                return new OkObjectResult(auditLogs);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting messages: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}