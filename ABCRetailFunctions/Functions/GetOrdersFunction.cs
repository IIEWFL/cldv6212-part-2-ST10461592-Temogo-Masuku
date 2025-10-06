using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetOrdersFunction
    {
        private readonly ILogger<GetOrdersFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetOrdersFunction(ILogger<GetOrdersFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetOrders")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequest req)
        {
            _logger.LogInformation("Getting all orders");

            try
            {
                var orders = await _tableStorageService.GetOrdersAsync();

                var orderDtos = orders.Select(o => new OrderDto
                {
                    PartitionKey = o.PartitionKey,
                    RowKey = o.RowKey,
                    Timestamp = o.Timestamp,
                    ETag = o.ETag.ToString(),
                    CustomerPartitionKey = o.CustomerPartitionKey,
                    CustomerRowKey = o.CustomerRowKey,
                    ProductPartitionKey = o.ProductPartitionKey,
                    ProductRowKey = o.ProductRowKey,
                    Quantity = o.Quantity,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus
                }).ToList();

                _logger.LogInformation($"Retrieved {orderDtos.Count} orders");
                return new OkObjectResult(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving orders: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}