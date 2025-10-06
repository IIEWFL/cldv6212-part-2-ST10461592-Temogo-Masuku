using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class DeleteProductFunction
    {
        private readonly ILogger<DeleteProductFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public DeleteProductFunction(
            ILogger<DeleteProductFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("DeleteProduct")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "products/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Deleting product: {partitionKey}/{rowKey}");

            try
            {
                // Get the product to retrieve the photo url
                var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    _logger.LogWarning($"Product not found: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Product not found");
                }

                // Delete the associated photo if it exists
                if (!string.IsNullOrEmpty(product.ProductPhotoUrl))
                {
                    await _blobStorageService.DeleteProductPhotoAsync(product.ProductPhotoUrl);
                }

                // Delete product from Table Storage
                await _tableStorageService.DeleteProductAsync(partitionKey, rowKey);

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Product Deleted",
                    EntityType = "Product",
                    Details = new
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        product.ProductName,
                        product.Price
                    }
                });

                _logger.LogInformation("Product deleted successfully");

                return new OkObjectResult(new { message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting product: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}