using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetProductsFunction
    {
        private readonly ILogger<GetProductsFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetProductsFunction(ILogger<GetProductsFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetProducts")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequest req)
        {
            _logger.LogInformation("Getting all products");

            try
            {
                var products = await _tableStorageService.GetProductsAsync();

                var productDtos = products.Select(p => new ProductDto
                {
                    PartitionKey = p.PartitionKey,
                    RowKey = p.RowKey,
                    Timestamp = p.Timestamp,
                    ETag = p.ETag.ToString(),
                    ProductName = p.ProductName,
                    Description = p.Description,
                    Price = p.Price,
                    Category = p.Category,
                    ProductPhotoUrl = p.ProductPhotoUrl
                }).ToList();

                _logger.LogInformation($"Retrieved {productDtos.Count} products");
                return new OkObjectResult(productDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving products: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}