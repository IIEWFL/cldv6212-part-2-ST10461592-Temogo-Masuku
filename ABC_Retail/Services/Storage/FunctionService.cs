using ABC_Retail.Models;
using System.Text;
using System.Text.Json;

namespace ABC_Retail.Services.Storage
{
    public class FunctionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _functionBaseUrl;

        public FunctionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _functionBaseUrl = configuration["AzureFunctionsBaseUrl"] ?? throw new InvalidOperationException("Azure Functions Base URL is missing");
            _functionBaseUrl = _functionBaseUrl.TrimEnd('/');
        }

        #region Customer Operations

        public async Task<List<Customer>> GetCustomersAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<Customer>>($"{_functionBaseUrl}/api/customers");
            return response ?? new List<Customer>();
        }

        public async Task<Customer?> GetCustomerAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.GetFromJsonAsync<Customer>($"{_functionBaseUrl}/api/customers/{partitionKey}/{rowKey}");
            return response;
        }

        public async Task<bool> CreateCustomerAsync(Customer customer, IFormFile? photo)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(customer.Name ?? string.Empty), "Name");
            content.Add(new StringContent(customer.Surname ?? string.Empty), "Surname");
            content.Add(new StringContent(customer.Email ?? string.Empty), "Email");
            content.Add(new StringContent(customer.PhoneNumber ?? string.Empty), "PhoneNumber");
            content.Add(new StringContent(customer.StreetAddress ?? string.Empty), "StreetAddress");
            content.Add(new StringContent(customer.City ?? string.Empty), "City");
            content.Add(new StringContent(customer.Province ?? string.Empty), "Province");
            content.Add(new StringContent(customer.PostalCode ?? string.Empty), "PostalCode");
            content.Add(new StringContent(customer.Country ?? string.Empty), "Country");

            // Add photo if provided
            if (photo != null)
            {
                var streamContent = new StreamContent(photo.OpenReadStream());
                content.Add(streamContent, "file", photo.FileName); // Changed from "photo" to "file"
            }

            var response = await _httpClient.PostAsync($"{_functionBaseUrl}/api/customers", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateCustomerAsync(string partitionKey, string rowKey, Customer customer, Stream? photoStream = null, string? photoFileName = null)
        {
            using var formData = new MultipartFormDataContent();

            formData.Add(new StringContent(customer.Name ?? string.Empty), "Name");
            formData.Add(new StringContent(customer.Surname ?? string.Empty), "Surname");
            formData.Add(new StringContent(customer.Email ?? string.Empty), "Email");
            formData.Add(new StringContent(customer.PhoneNumber ?? string.Empty), "PhoneNumber");
            formData.Add(new StringContent(customer.StreetAddress ?? string.Empty), "StreetAddress");
            formData.Add(new StringContent(customer.City ?? string.Empty), "City");
            formData.Add(new StringContent(customer.Province ?? string.Empty), "Province");
            formData.Add(new StringContent(customer.PostalCode ?? string.Empty), "PostalCode");
            formData.Add(new StringContent(customer.Country ?? string.Empty), "Country");

            if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
            {
                var streamContent = new StreamContent(photoStream);
                formData.Add(streamContent, "file", photoFileName);
            }

            var response = await _httpClient.PutAsync($"{_functionBaseUrl}/api/customers/{partitionKey}/{rowKey}", formData);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.DeleteAsync($"{_functionBaseUrl}/api/customers/{partitionKey}/{rowKey}"); 
            return response.IsSuccessStatusCode;
        }

        #endregion

        #region Product Operations

        public async Task<List<Product>> GetProductsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<Product>>($"{_functionBaseUrl}/api/products");
            return response ?? new List<Product>();
        }

        public async Task<Product?> GetProductAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.GetFromJsonAsync<Product>($"{_functionBaseUrl}/api/products/{partitionKey}/{rowKey}");
            return response;
        }

        public async Task<bool> CreateProductAsync(Product product, IFormFile? photo)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(product.ProductName ?? string.Empty), "ProductName");
            content.Add(new StringContent(product.Description ?? string.Empty), "Description");
            content.Add(new StringContent(product.Price.ToString()), "Price");
            content.Add(new StringContent(product.Category ?? string.Empty), "Category");

            // Add photo if provided
            if (photo != null)
            {
                var streamContent = new StreamContent(photo.OpenReadStream());
                content.Add(streamContent, "file", photo.FileName);
            }

            var response = await _httpClient.PostAsync($"{_functionBaseUrl}/api/products", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateProductAsync(string partitionKey, string rowKey, Product product, Stream? photoStream = null, string? photoFileName = null)
        {
            using var formData = new MultipartFormDataContent();

            formData.Add(new StringContent(product.ProductName ?? string.Empty), "ProductName");
            formData.Add(new StringContent(product.Description ?? string.Empty), "Description");
            formData.Add(new StringContent(product.Price.ToString()), "Price");
            formData.Add(new StringContent(product.Category ?? string.Empty), "Category");

            if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
            {
                var streamContent = new StreamContent(photoStream);
                formData.Add(streamContent, "file", photoFileName);
            }

            var response = await _httpClient.PutAsync($"{_functionBaseUrl}/api/products/{partitionKey}/{rowKey}", formData);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteProductAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.DeleteAsync($"{_functionBaseUrl}/api/products/{partitionKey}/{rowKey}");
            return response.IsSuccessStatusCode;
        }

        #endregion

        #region Order Operations

        public async Task<List<Order>> GetOrdersAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<Order>>($"{_functionBaseUrl}/api/orders");
            return response ?? new List<Order>();
        }

        public async Task<Order?> GetOrderAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.GetFromJsonAsync<Order>($"{_functionBaseUrl}/api/orders/{partitionKey}/{rowKey}");
            return response;
        }

        public async Task<bool> CreateOrderAsync(Order order)
        {
            var json = JsonSerializer.Serialize(order);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionBaseUrl}/api/orders", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateOrderAsync(string partitionKey, string rowKey, Order order)
        {
            var json = JsonSerializer.Serialize(order);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_functionBaseUrl}/api/orders/{partitionKey}/{rowKey}", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteOrderAsync(string partitionKey, string rowKey)
        {
            var response = await _httpClient.DeleteAsync($"{_functionBaseUrl}/api/orders/{partitionKey}/{rowKey}");
            return response.IsSuccessStatusCode;
        }

        #endregion

        #region Queue Operations

        public async Task<List<AuditLog>> GetMessagesAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<AuditLog>>($"{_functionBaseUrl}/api/queue/messages");
            return response ?? new List<AuditLog>();
        }

        public async Task<bool> UploadAuditLogAsync(object logData)
        {
            var json = JsonSerializer.Serialize(logData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_functionBaseUrl}/api/queue/auditlog", content);
            return response.IsSuccessStatusCode;
        }

        #endregion

        #region File Share Operations

        public async Task<bool> UploadFileAsync(string fileName, Stream fileStream)
        {
            using var formData = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            formData.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"{_functionBaseUrl}/api/fileshare/upload", formData);
            return response.IsSuccessStatusCode;
        }

        #endregion
    }
}