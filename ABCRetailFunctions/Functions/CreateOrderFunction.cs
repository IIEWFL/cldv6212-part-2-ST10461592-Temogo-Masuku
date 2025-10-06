using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetailFunctions.Functions
{
    public class CreateOrderFunction
    {
        private readonly ILogger<CreateOrderFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly QueueStorageService _queueStorageService;

        public CreateOrderFunction(
            ILogger<CreateOrderFunction> logger,
            TableStorageService tableStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("CreateOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req)
        {
            _logger.LogInformation("Creating a new order");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var orderDto = JsonSerializer.Deserialize<OrderDto>(requestBody);

                if (orderDto == null || string.IsNullOrEmpty(orderDto.CustomerPartitionKey) || string.IsNullOrEmpty(orderDto.ProductPartitionKey))
                {
                    return new BadRequestObjectResult("Customer and Product information are required");
                }

                // Validate quantity
                if (orderDto.Quantity <= 0)
                {
                    return new BadRequestObjectResult("Quantity must be greater than zero");
                }

                var order = new Order
                {
                    CustomerPartitionKey = orderDto.CustomerPartitionKey,
                    CustomerRowKey = orderDto.CustomerRowKey,
                    ProductPartitionKey = orderDto.ProductPartitionKey,
                    ProductRowKey = orderDto.ProductRowKey,
                    Quantity = orderDto.Quantity,
                    OrderDate = orderDto.OrderDate,
                    TotalAmount = orderDto.TotalAmount,
                    OrderStatus = orderDto.OrderStatus ?? "Pending"
                };

                // Insert into Table Storage
                await _tableStorageService.InsertOrderAsync(order);

                _logger.LogInformation($"Order created: PartitionKey: {order.PartitionKey}, RowKey: {order.RowKey}");

                // Send audit log to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Order Created",
                    EntityType = "Order",
                    Details = new
                    {
                        order.PartitionKey,
                        order.RowKey,
                        order.CustomerPartitionKey,
                        order.CustomerRowKey,
                        order.ProductPartitionKey,
                        order.ProductRowKey,
                        order.Quantity,
                        order.TotalAmount,
                        order.OrderStatus
                    }
                });

                return new OkObjectResult(new
                {
                    message = "Order created successfully",
                    partitionKey = order.PartitionKey,
                    rowKey = order.RowKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating order: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}