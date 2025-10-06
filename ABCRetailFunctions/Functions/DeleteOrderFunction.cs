using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class DeleteOrderFunction
    {
        private readonly ILogger<DeleteOrderFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly QueueStorageService _queueStorageService;

        public DeleteOrderFunction(
            ILogger<DeleteOrderFunction> logger,
            TableStorageService tableStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("DeleteOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Deleting order: {partitionKey}/{rowKey}");

            try
            {
                // Get the order to retrieve details for logging
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    _logger.LogWarning($"Order not found: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Order not found");
                }

                // Delete order from Table Storage
                await _tableStorageService.DeleteOrderAsync(partitionKey, rowKey);

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Order Deleted",
                    EntityType = "Order",
                    Details = new
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        order.CustomerPartitionKey,
                        order.CustomerRowKey,
                        order.ProductPartitionKey,
                        order.ProductRowKey,
                        order.Quantity,
                        order.TotalAmount,
                        order.OrderStatus
                    }
                });

                _logger.LogInformation("Order deleted successfully");

                return new OkObjectResult(new { message = "Order deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting order: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}