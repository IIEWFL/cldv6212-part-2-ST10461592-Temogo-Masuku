using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class UpdateCustomerFunction
    {
        private readonly ILogger<UpdateCustomerFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public UpdateCustomerFunction(
            ILogger<UpdateCustomerFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("UpdateCustomer")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customers/{partitionKey}/{rowKey}")] HttpRequest req,
            string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Updating customer: {partitionKey}/{rowKey}");

            try
            {
                // Read form data
                var formData = await req.ReadFormAsync();
                var name = formData["Name"].ToString();
                var surname = formData["Surname"].ToString();
                var email = formData["Email"].ToString();
                var phoneNumber = formData["PhoneNumber"].ToString();
                var streetAddress = formData["StreetAddress"].ToString();
                var city = formData["City"].ToString();
                var province = formData["Province"].ToString();
                var postalCode = formData["PostalCode"].ToString();
                var country = formData["Country"].ToString();

                // Get existing customer
                var existingCustomer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);
                if (existingCustomer == null)
                {
                    _logger.LogWarning($"Customer not found for PartitionKey: {partitionKey}, RowKey: {rowKey}");
                    return new NotFoundObjectResult("Customer not found");
                }

                // Update customer fields
                if (!string.IsNullOrEmpty(name)) existingCustomer.Name = name;
                if (!string.IsNullOrEmpty(surname)) existingCustomer.Surname = surname;
                if (!string.IsNullOrEmpty(email)) existingCustomer.Email = email;
                if (!string.IsNullOrEmpty(phoneNumber)) existingCustomer.PhoneNumber = phoneNumber;
                if (!string.IsNullOrEmpty(streetAddress)) existingCustomer.StreetAddress = streetAddress;
                if (!string.IsNullOrEmpty(city)) existingCustomer.City = city;
                if (!string.IsNullOrEmpty(province)) existingCustomer.Province = province;
                if (!string.IsNullOrEmpty(postalCode)) existingCustomer.PostalCode = postalCode;
                if (!string.IsNullOrEmpty(country)) existingCustomer.Country = country;

                // Handle photo upload if provided
                if (formData.Files.Count > 0)
                {
                    var photo = formData.Files[0];

                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(existingCustomer.CustomerPhotoUrl))
                    {
                        await _blobStorageService.DeleteCustomerPhotoAsync(existingCustomer.CustomerPhotoUrl);
                    }

                    // Upload new photo
                    using var stream = photo.OpenReadStream();
                    existingCustomer.CustomerPhotoUrl = await _blobStorageService.UploadCustomerPhotoAsync(photo.FileName, stream);
                }

                // Save the updated customer entity
                await _tableStorageService.UpdateCustomerAsync(existingCustomer);

                _logger.LogInformation("Customer updated successfully");

                // Send message to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Customer Updated",
                    EntityType = "Customer",
                    Details = new
                    {
                        existingCustomer.PartitionKey,
                        existingCustomer.RowKey,
                        existingCustomer.Name,
                        existingCustomer.Email
                    }
                });

                return new OkObjectResult(new { message = "Customer updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating customer: PartitionKey: {partitionKey}, RowKey: {rowKey}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}