using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using System.ComponentModel.DataAnnotations;

namespace ABC_Retail.Services
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetAllCustomersAsync();
        Task<Customer?> GetCustomerByIdAsync(string partitionKey, string rowKey);
        Task<Customer> CreateCustomerAsync(Customer customer, Stream? photoStream = null, string? photoFileName = null);
        Task<Customer> UpdateCustomerAsync(Customer customer, Stream? photoStream = null, string? photoFileName = null);
        Task<bool> DeleteCustomerAsync(string partitionKey, string rowKey);
        Task<IEnumerable<Customer>> GetCustomersByProvinceAsync(string province);
        Task<bool> IsEmailUniqueAsync(string email, string? excludePartitionKey = null, string? excludeRowKey = null);
        Task<ValidationResult> ValidateCustomerAsync(Customer customer);
    }

    public class CustomerService : ICustomerService
    {
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public CustomerService(
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            try
            {
                return await _tableStorageService.GetCustomersAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync("GetAllCustomers", ex);
                throw new InvalidOperationException("Failed to retrieve customers", ex);
            }
        }

        public async Task<Customer?> GetCustomerByIdAsync(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
                return null;

            try
            {
                return await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetCustomerById - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to retrieve customer", ex);
            }
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer, Stream? photoStream = null, string? photoFileName = null)
        {
            // Validate customer
            var validationResult = await ValidateCustomerAsync(customer);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            // Check email uniqueness
            if (!await IsEmailUniqueAsync(customer.Email!))
            {
                throw new ArgumentException("Email address already exists");
            }

            try
            {
                // Handle photo upload if provided
                if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    customer.CustomerPhotoUrl = await _blobStorageService.UploadCustomerPhotoAsync(photoFileName, photoStream);
                }

                // Set default values
                customer.PartitionKey = customer.Province ?? "Unknown";
                customer.RowKey = Guid.NewGuid().ToString();

                await _tableStorageService.InsertCustomerAsync(customer);

                // Log the creation
                await LogCustomerActionAsync("Customer Created", customer);

                return customer;
            }
            catch (Exception ex)
            {
                await LogErrorAsync("CreateCustomer", ex);
                throw new InvalidOperationException("Failed to create customer", ex);
            }
        }

        public async Task<Customer> UpdateCustomerAsync(Customer customer, Stream? photoStream = null, string? photoFileName = null)
        {
            // Validate customer
            var validationResult = await ValidateCustomerAsync(customer);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            // Check email uniqueness (excluding current customer)
            if (!await IsEmailUniqueAsync(customer.Email!, customer.PartitionKey, customer.RowKey))
            {
                throw new ArgumentException("Email address already exists");
            }

            try
            {
                // Handle photo upload if new photo provided
                if (photoStream != null && !string.IsNullOrEmpty(photoFileName))
                {
                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(customer.CustomerPhotoUrl))
                    {
                        await _blobStorageService.DeleteCustomerPhotoAsync(customer.CustomerPhotoUrl);
                    }

                    customer.CustomerPhotoUrl = await _blobStorageService.UploadCustomerPhotoAsync(photoFileName, photoStream);
                }

                await _tableStorageService.UpdateCustomerAsync(customer);

                // Log the update
                await LogCustomerActionAsync("Customer Updated", customer);

                return customer;
            }
            catch (Exception ex)
            {
                await LogErrorAsync("UpdateCustomer", ex);
                throw new InvalidOperationException("Failed to update customer", ex);
            }
        }

        public async Task<bool> DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            try
            {
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                    return false;

                // Delete customer photo if exists
                if (!string.IsNullOrEmpty(customer.CustomerPhotoUrl))
                {
                    await _blobStorageService.DeleteCustomerPhotoAsync(customer.CustomerPhotoUrl);
                }

                await _tableStorageService.DeleteCustomerAsync(partitionKey, rowKey);

                // Log the deletion
                await LogCustomerActionAsync("Customer Deleted", customer);

                return true;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"DeleteCustomer - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to delete customer", ex);
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersByProvinceAsync(string province)
        {
            try
            {
                var allCustomers = await _tableStorageService.GetCustomersAsync();
                return allCustomers.Where(c => string.Equals(c.Province, province, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetCustomersByProvince - {province}", ex);
                throw new InvalidOperationException("Failed to retrieve customers by province", ex);
            }
        }

        public async Task<bool> IsEmailUniqueAsync(string email, string? excludePartitionKey = null, string? excludeRowKey = null)
        {
            try
            {
                var allCustomers = await _tableStorageService.GetCustomersAsync();
                return !allCustomers.Any(c =>
                    string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase) &&
                    !(c.PartitionKey == excludePartitionKey && c.RowKey == excludeRowKey));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"IsEmailUnique - {email}", ex);
                return false; // Assume not unique on error for safety
            }
        }

        public Task<ValidationResult> ValidateCustomerAsync(Customer customer)
        {
            var context = new ValidationContext(customer);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(customer, context, results, true))
            {
                return Task.FromResult(results.First());
            }

            // Additional business rules
            if (!string.IsNullOrEmpty(customer.Email) &&
                !customer.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ValidationResult("Only Gmail addresses are allowed"));
            }

            return Task.FromResult(ValidationResult.Success!);
        }

        private async Task LogCustomerActionAsync(string action, Customer customer)
        {
            try
            {
                var logEntry = new
                {
                    Action = action,
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Customer",
                    Details = new
                    {
                        customer.PartitionKey,
                        customer.RowKey,
                        customer.Name,
                        customer.Surname,
                        customer.Email,
                        customer.Province
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don\'t fail the main operation if logging fails
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
                    EntityType = "Customer",
                    Operation = operation,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don\'t fail the main operation if logging fails
            }
        }
    }
}