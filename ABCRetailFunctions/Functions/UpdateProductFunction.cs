using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class UpdateProductFunction
    {
        private readonly ILogger<UpdateProductFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public UpdateProductFunction(
            ILogger<UpdateProductFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("UpdateProduct")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "products/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Updating product: {partitionKey}/{rowKey}");

            try
            {
                // Read form data
                var formData = await req.ReadFormAsync();
                var productName = formData["ProductName"].ToString();
                var description = formData["Description"].ToString();
                var price = formData["Price"].ToString();
                var category = formData["Category"].ToString();

                // Get existing product
                var existingProduct = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
                if (existingProduct == null)
                {
                    _logger.LogWarning($"Product not found for PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Product not found");
                }

                // Update product fields
                if (!string.IsNullOrEmpty(productName)) existingProduct.ProductName = productName;
                if (!string.IsNullOrEmpty(description)) existingProduct.Description = description;
                if (!string.IsNullOrEmpty(price) && double.TryParse(price, out var parsedPrice))
                    existingProduct.Price = parsedPrice;
                if (!string.IsNullOrEmpty(category)) existingProduct.Category = category;

                // Handle photo upload if provided
                if (formData.Files.Count > 0)
                {
                    var photo = formData.Files[0];

                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(existingProduct.ProductPhotoUrl))
                    {
                        await _blobStorageService.DeleteProductPhotoAsync(existingProduct.ProductPhotoUrl);
                    }

                    // Upload new photo
                    using var stream = photo.OpenReadStream();
                    existingProduct.ProductPhotoUrl = await _blobStorageService.UploadProductPhotoAsync(photo.FileName, stream);
                }

                // Save the updated product entity
                await _tableStorageService.UpdateProductAsync(existingProduct);

                _logger.LogInformation("Product updated successfully");

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Product Updated",
                    EntityType = "Product",
                    Details = new
                    {
                        existingProduct.PartitionKey,
                        existingProduct.RowKey,
                        existingProduct.ProductName,
                        existingProduct.Price
                    }
                });

                return new OkObjectResult(new { message = "Product updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating product: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}