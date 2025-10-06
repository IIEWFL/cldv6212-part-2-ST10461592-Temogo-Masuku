using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetailFunctions.Functions
{
    public class UpdateOrderFunction
    {
        private readonly ILogger<UpdateOrderFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly QueueStorageService _queueStorageService;

        public UpdateOrderFunction(
            ILogger<UpdateOrderFunction> logger,
            TableStorageService tableStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("UpdateOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Updating order: {partitionKey}/{rowKey}");

            try
            {
                // Read JSON data (orders don't have files)
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var orderDto = JsonSerializer.Deserialize<OrderDto>(requestBody);

                if (orderDto == null)
                {
                    return new BadRequestObjectResult("Order data is required");
                }

                // Get existing order
                var existingOrder = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (existingOrder == null)
                {
                    _logger.LogWarning($"Order not found for PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Order not found");
                }

                // Update order fields
                existingOrder.CustomerPartitionKey = orderDto.CustomerPartitionKey;
                existingOrder.CustomerRowKey = orderDto.CustomerRowKey;
                existingOrder.ProductPartitionKey = orderDto.ProductPartitionKey;
                existingOrder.ProductRowKey = orderDto.ProductRowKey;
                existingOrder.Quantity = orderDto.Quantity;
                existingOrder.TotalAmount = orderDto.TotalAmount;
                existingOrder.OrderStatus = orderDto.OrderStatus;

                // Save the updated order entity
                await _tableStorageService.UpdateOrderAsync(existingOrder);

                _logger.LogInformation("Order updated successfully");

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Order Updated",
                    EntityType = "Order",
                    Details = new
                    {
                        existingOrder.PartitionKey,
                        existingOrder.RowKey,
                        existingOrder.Quantity,
                        existingOrder.TotalAmount,
                        existingOrder.OrderStatus
                    }
                });

                return new OkObjectResult(new { message = "Order updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating order: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}