using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ABCRetailFunctions.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _customerPhotoContainer;
        private readonly BlobContainerClient _productPhotoContainer;

        public BlobStorageService(string storageConnectionString, string serviceIdentifier)
        {
            _blobServiceClient = new BlobServiceClient(storageConnectionString);

            // Create separate containers for customer and product photos
            _customerPhotoContainer = _blobServiceClient.GetBlobContainerClient("customer-photos");
            _productPhotoContainer = _blobServiceClient.GetBlobContainerClient("product-photos");

            // Create containers if they don't exist with public blob access
            _customerPhotoContainer.CreateIfNotExists(PublicAccessType.Blob);
            _productPhotoContainer.CreateIfNotExists(PublicAccessType.Blob);
        }

        // Upload customer photo
        public async Task<string> UploadCustomerPhotoAsync(string fileName, Stream fileStream)
        {
            try
            {
                // Generate unique filename to avoid conflicts
                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var blobClient = _customerPhotoContainer.GetBlobClient(uniqueFileName);

                // Set content type based on file extension
                var contentType = GetContentType(fileName);
                var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };

                // Upload the file
                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload customer photo: {ex.Message}", ex);
            }
        }

        // Upload product photo
        public async Task<string> UploadProductPhotoAsync(string fileName, Stream fileStream)
        {
            try
            {
                // Generate unique filename to avoid conflicts
                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var blobClient = _productPhotoContainer.GetBlobClient(uniqueFileName);

                // Set content type based on file extension
                var contentType = GetContentType(fileName);
                var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };

                // Upload the file
                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload product photo: {ex.Message}", ex);
            }
        }

        // Delete customer photo
        public async Task<bool> DeleteCustomerPhotoAsync(string photoUrl)
        {
            try
            {
                var blobName = GetBlobNameFromUrl(photoUrl);
                if (string.IsNullOrEmpty(blobName))
                    return false;

                var blobClient = _customerPhotoContainer.GetBlobClient(blobName);
                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete customer photo: {ex.Message}", ex);
            }
        }

        // Delete product photo
        public async Task<bool> DeleteProductPhotoAsync(string photoUrl)
        {
            try
            {
                var blobName = GetBlobNameFromUrl(photoUrl);
                if (string.IsNullOrEmpty(blobName))
                    return false;

                var blobClient = _productPhotoContainer.GetBlobClient(blobName);
                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete product photo: {ex.Message}", ex);
            }
        }

        // Get all customer photos
        public async Task<List<string>> GetAllCustomerPhotosAsync()
        {
            var photoUrls = new List<string>();

            await foreach (var blobItem in _customerPhotoContainer.GetBlobsAsync())
            {
                var blobClient = _customerPhotoContainer.GetBlobClient(blobItem.Name);
                photoUrls.Add(blobClient.Uri.ToString());
            }

            return photoUrls;
        }

        // Get all product photos
        public async Task<List<string>> GetAllProductPhotosAsync()
        {
            var photoUrls = new List<string>();

            await foreach (var blobItem in _productPhotoContainer.GetBlobsAsync())
            {
                var blobClient = _productPhotoContainer.GetBlobClient(blobItem.Name);
                photoUrls.Add(blobClient.Uri.ToString());
            }

            return photoUrls;
        }

        // Helper method to get content type based on file extension
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        // Helper method to extract blob name from URL
        private string GetBlobNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments;
                return segments.LastOrDefault()?.TrimStart('/') ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Check if customer photo exists
        public async Task<bool> CustomerPhotoExistsAsync(string photoUrl)
        {
            try
            {
                var blobName = GetBlobNameFromUrl(photoUrl);
                if (string.IsNullOrEmpty(blobName))
                    return false;

                var blobClient = _customerPhotoContainer.GetBlobClient(blobName);
                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch
            {
                return false;
            }
        }

        // Check if product photo exists
        public async Task<bool> ProductPhotoExistsAsync(string photoUrl)
        {
            try
            {
                var blobName = GetBlobNameFromUrl(photoUrl);
                if (string.IsNullOrEmpty(blobName))
                    return false;

                var blobClient = _productPhotoContainer.GetBlobClient(blobName);
                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch
            {
                return false;
            }
        }
    }
}