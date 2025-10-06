using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using System.ComponentModel.DataAnnotations;

namespace ABC_Retail.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(string partitionKey, string rowKey);
        Task<Product> CreateProductAsync(Product product, Stream? photoStream = null, string? photoFileName = null);
        Task<Product> UpdateProductAsync(Product product, Stream? photoStream = null, string? photoFileName = null);
        Task<bool> DeleteProductAsync(string partitionKey, string rowKey);
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
        Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(double minPrice, double maxPrice);
        Task<bool> IsProductNameUniqueAsync(string productName, string? excludePartitionKey = null, string? excludeRowKey = null);
        Task<ValidationResult> ValidateProductAsync(Product product);
    }

    public class ProductService : IProductService
    {
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public ProductService(
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            try
            {
                return await _tableStorageService.GetProductsAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync("GetAllProducts", ex);
                throw new InvalidOperationException("Failed to retrieve products", ex);
            }
        }

        public async Task<Product?> GetProductByIdAsync(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
                return null;

            try
            {
                return await _tableStorageService.GetProductAsync(partitionKey, rowKey);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetProductById - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to retrieve product", ex);
            }
        }

        public async Task<Product> CreateProductAsync(Product product, Stream? photoStream = null, string? photoFileName = null)
        {
            // Validate product
            var validationResult = await ValidateProductAsync(product);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            // Check product name uniqueness
            if (!await IsProductNameUniqueAsync(product.ProductName))
            {
                throw new ArgumentException("Product name already exists");
            }

            try
            {
                // Set default values
                product.PartitionKey = product.Category ?? "General";
                product.RowKey = Guid.NewGuid().ToString();

                // Handle photo upload
                if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    product.ProductPhotoUrl = await _blobStorageService.UploadProductPhotoAsync(photoFileName, photoStream);
                }

                await _tableStorageService.InsertProductAsync(product);

                // Log the creation
                await LogProductActionAsync("Product Created", product);

                return product;
            }
            catch (Exception ex)
            {
                await LogErrorAsync("CreateProduct", ex);
                throw new InvalidOperationException("Failed to create product", ex);
            }
        }

        public async Task<Product> UpdateProductAsync(Product product, Stream? photoStream = null, string? photoFileName = null)
        {
            // Validate product
            var validationResult = await ValidateProductAsync(product);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            // Check product name uniqueness (excluding current product)
            if (!await IsProductNameUniqueAsync(product.ProductName, product.PartitionKey, product.RowKey))
            {
                throw new ArgumentException("Product name already exists");
            }

            try
            {
                // Handle photo upload
                if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(product.ProductPhotoUrl))
                    {
                        await _blobStorageService.DeleteProductPhotoAsync(product.ProductPhotoUrl);
                    }

                    product.ProductPhotoUrl = await _blobStorageService.UploadProductPhotoAsync(photoFileName, photoStream);
                }

                await _tableStorageService.UpdateProductAsync(product);

                // Log the update
                await LogProductActionAsync("Product Updated", product);

                return product;
            }
            catch (Exception ex)
            {
                await LogErrorAsync("UpdateProduct", ex);
                throw new InvalidOperationException("Failed to update product", ex);
            }
        }

        public async Task<bool> DeleteProductAsync(string partitionKey, string rowKey)
        {
            try
            {
                var product = await _tableStorageService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                    return false;

                // Delete product photo if exists
                if (!string.IsNullOrEmpty(product.ProductPhotoUrl))
                {
                    await _blobStorageService.DeleteProductPhotoAsync(product.ProductPhotoUrl);
                }

                await _tableStorageService.DeleteProductAsync(partitionKey, rowKey);

                // Log the deletion
                await LogProductActionAsync("Product Deleted", product);

                return true;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"DeleteProduct - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to delete product", ex);
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
        {
            try
            {
                var allProducts = await _tableStorageService.GetProductsAsync();
                return allProducts.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetProductsByCategory - {category}", ex);
                throw new InvalidOperationException("Failed to retrieve products by category", ex);
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(double minPrice, double maxPrice)
        {
            try
            {
                var allProducts = await _tableStorageService.GetProductsAsync();
                return allProducts.Where(p => p.Price >= minPrice && p.Price <= maxPrice);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetProductsByPriceRange - {minPrice}-{maxPrice}", ex);
                throw new InvalidOperationException("Failed to retrieve products by price range", ex);
            }
        }

        public async Task<bool> IsProductNameUniqueAsync(string productName, string? excludePartitionKey = null, string? excludeRowKey = null)
        {
            try
            {
                var allProducts = await _tableStorageService.GetProductsAsync();
                return !allProducts.Any(p =>
                    string.Equals(p.ProductName, productName, StringComparison.OrdinalIgnoreCase) &&
                    !(p.PartitionKey == excludePartitionKey && p.RowKey == excludeRowKey));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"IsProductNameUnique - {productName}", ex);
                return false; // Assume not unique on error for safety
            }
        }

        public Task<ValidationResult> ValidateProductAsync(Product product)
        {
            var context = new ValidationContext(product);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(product, context, results, true))
            {
                return Task.FromResult(results.First());
            }

            // Additional business rules
            if (product.Price < 0)
            {
                return Task.FromResult(new ValidationResult("Price cannot be negative"));
            }

            if (product.Price > 1000000)
            {
                return Task.FromResult(new ValidationResult("Price cannot exceed R1,000,000"));
            }

            return Task.FromResult(ValidationResult.Success!);
        }

        private async Task LogProductActionAsync(string action, Product product)
        {
            try
            {
                var logEntry = new
                {
                    Action = action,
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Product",
                    Details = new
                    {
                        product.PartitionKey,
                        product.RowKey,
                        product.ProductName,
                        product.Category,
                        product.Price
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don't fail the main operation if logging fails
            }
        }

        private async Task LogErrorAsync(string operation, Exception ex)
        {
            try
            {
                var logEntry = new
                {
                    Action = "Error",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Product",
                    Operation = operation,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don't fail the main operation if logging fails
            }
        }
    }
}