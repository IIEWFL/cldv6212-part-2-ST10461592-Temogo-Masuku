using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class CreateProductFunction
    {
        private readonly ILogger<CreateProductFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public CreateProductFunction(
            ILogger<CreateProductFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("CreateProduct")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequest req)
        {
            _logger.LogInformation("Creating a new product");

            try
            {
                var form = await req.ReadFormAsync();

                // Read form data
                var product = new Product
                {
                    ProductName = form["ProductName"],
                    Description = form["Description"],
                    Price = double.Parse(form["Price"]),
                    Category = form["Category"]
                };

                // Validate required fields
                if (string.IsNullOrEmpty(product.ProductName))
                {
                    return new BadRequestObjectResult("ProductName is required");
                }

                // Handle photo upload if provided
                if (req.Form.Files.Count > 0)
                {
                    var photo = req.Form.Files[0];
                    using var stream = photo.OpenReadStream();
                    product.ProductPhotoUrl = await _blobStorageService.UploadProductPhotoAsync(photo.FileName, stream);
                }

                // Insert into Table Storage
                await _tableStorageService.InsertProductAsync(product);

                _logger.LogInformation($"Product created: {product.ProductName}");

                // Send audit log to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Product Created",
                    EntityType = "Product",
                    Details = new
                    {
                        product.PartitionKey,
                        product.RowKey,
                        product.ProductName,
                        product.Price
                    }
                });

                return new OkObjectResult(new
                {
                    message = "Product created successfully",
                    partitionKey = product.PartitionKey,
                    rowKey = product.RowKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating product: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}