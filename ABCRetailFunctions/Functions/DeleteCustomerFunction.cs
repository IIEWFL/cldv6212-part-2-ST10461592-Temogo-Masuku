using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class DeleteCustomerFunction
    {
        private readonly ILogger<DeleteCustomerFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public DeleteCustomerFunction(
            ILogger<DeleteCustomerFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("DeleteCustomer")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Deleting customer: {partitionKey}/{rowKey}");

            try
            {
                // Get the customer to retrieve the photo url
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    _logger.LogWarning($"Customer not found: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Customer not found");
                }

                // Delete the associated photo if it exists
                if (!string.IsNullOrEmpty(customer.CustomerPhotoUrl))
                {
                    await _blobStorageService.DeleteCustomerPhotoAsync(customer.CustomerPhotoUrl);
                }

                // Delete customer from Table Storage
                await _tableStorageService.DeleteCustomerAsync(partitionKey, rowKey);

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Customer Deleted",
                    EntityType = "Customer",
                    Details = new
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        customer.Name,
                        customer.Email
                    }
                });

                _logger.LogInformation("Customer deleted successfully");

                return new OkObjectResult(new { message = "Customer deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting customer: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}