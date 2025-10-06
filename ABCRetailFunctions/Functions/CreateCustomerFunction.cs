using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class CreateCustomerFunction
    {
        private readonly ILogger<CreateCustomerFunction> _logger;
        private readonly TableStorageService _tableStorageService;
        private readonly BlobStorageService _blobStorageService;
        private readonly QueueStorageService _queueStorageService;

        public CreateCustomerFunction(
            ILogger<CreateCustomerFunction> logger,
            TableStorageService tableStorageService,
            BlobStorageService blobStorageService,
            QueueStorageService queueStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
        }

        [Function("CreateCustomer")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequest req)
        {
            _logger.LogInformation("Creating a new customer");

            try
            {
                var form = await req.ReadFormAsync();

                // Read form data
                var customer = new Customer
                {
                    Name = form["Name"],
                    Surname = form["Surname"],
                    Email = form["Email"],
                    PhoneNumber = form["PhoneNumber"],
                    StreetAddress = form["StreetAddress"],
                    City = form["City"],
                    Province = form["Province"],
                    PostalCode = form["PostalCode"],
                    Country = form["Country"]
                };

                // Validate required fields
                if (string.IsNullOrEmpty(customer.Name) || string.IsNullOrEmpty(customer.Email))
                {
                    return new BadRequestObjectResult("Name and Email are required");
                }

                // Handle photo upload if provided
                if (req.Form.Files.Count > 0)
                {
                    var photo = req.Form.Files[0];
                    using var stream = photo.OpenReadStream();
                    customer.CustomerPhotoUrl = await _blobStorageService.UploadCustomerPhotoAsync(photo.FileName, stream);
                }

                // Insert into Table Storage
                await _tableStorageService.InsertCustomerAsync(customer);

                _logger.LogInformation($"Customer created: {customer.Name} with PartitionKey: {customer.PartitionKey}, RowKey: {customer.RowKey}");

                // Send audit log to queue
                await _queueStorageService.SendLogEntryAsync(new
                {
                    Action = "Customer Created",
                    EntityType = "Customer",
                    Details = new
                    {
                        customer.PartitionKey,
                        customer.RowKey,
                        customer.Name,
                        customer.Email
                    }
                });

                return new OkObjectResult(new
                {
                    message = "Customer created successfully",
                    partitionKey = customer.PartitionKey,
                    rowKey = customer.RowKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating customer: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}