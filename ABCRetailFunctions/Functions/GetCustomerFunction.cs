using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetCustomerFunction
    {
        private readonly ILogger<GetCustomerFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetCustomerFunction(ILogger<GetCustomerFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetCustomer")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{partitionKey}/{rowKey}")] HttpRequest req, string partitionKey, string rowKey)
        {
            _logger.LogInformation($"Getting customer: {partitionKey}/{rowKey}");

            try
            {
                var customer = await _tableStorageService.GetCustomerAsync(partitionKey, rowKey);

                if (customer == null)
                {
                    return new NotFoundObjectResult("Customer not found");
                }

                var customerDto = new CustomerDto
                {
                    PartitionKey = customer.PartitionKey,
                    RowKey = customer.RowKey,
                    Timestamp = customer.Timestamp,
                    ETag = customer.ETag.ToString(),
                    Name = customer.Name,
                    Surname = customer.Surname,
                    Email = customer.Email,
                    PhoneNumber = customer.PhoneNumber,
                    StreetAddress = customer.StreetAddress,
                    City = customer.City,
                    Province = customer.Province,
                    PostalCode = customer.PostalCode,
                    Country = customer.Country,
                    CustomerPhotoUrl = customer.CustomerPhotoUrl
                };

                return new OkObjectResult(customerDto);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving customer: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}