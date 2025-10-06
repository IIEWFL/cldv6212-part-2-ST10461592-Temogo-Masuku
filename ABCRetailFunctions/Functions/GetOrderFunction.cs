using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetOrderFunction
    {
        private readonly ILogger<GetOrderFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetOrderFunction(ILogger<GetOrderFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Getting order: {partitionKey}/{rowKey}");

            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);

                if (order == null)
                {
                    return new NotFoundObjectResult("Order not found");
                }

                var orderDto = new OrderDto
                {
                    PartitionKey = order.PartitionKey,
                    RowKey = order.RowKey,
                    Timestamp = order.Timestamp,
                    ETag = order.ETag.ToString(),
                    CustomerPartitionKey = order.CustomerPartitionKey,
                    CustomerRowKey = order.CustomerRowKey,
                    ProductPartitionKey = order.ProductPartitionKey,
                    ProductRowKey = order.ProductRowKey,
                    Quantity = order.Quantity,
                    OrderDate = order.OrderDate,
                    TotalAmount = order.TotalAmount,
                    OrderStatus = order.OrderStatus
                };

                return new OkObjectResult(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving order: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}