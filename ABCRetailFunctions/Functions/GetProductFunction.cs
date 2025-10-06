using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetProductFunction
    {
        private readonly ILogger<GetProductFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetProductFunction(ILogger<GetProductFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetProduct")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Getting product: {partitionKey}/{rowKey}");

            try
            {
                var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);

                if (product == null)
                {
                    return new NotFoundObjectResult("Product not found");
                }

                var productDto = new ProductDto
                {
                    PartitionKey = product.PartitionKey,
                    RowKey = product.RowKey,
                    Timestamp = product.Timestamp,
                    ETag = product.ETag.ToString(),
                    ProductName = product.ProductName,
                    Description = product.Description,
                    Price = product.Price,
                    Category = product.Category,
                    ProductPhotoUrl = product.ProductPhotoUrl
                };

                return new OkObjectResult(productDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving product: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}