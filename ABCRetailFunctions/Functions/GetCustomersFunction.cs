using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class GetCustomersFunction
    {
        private readonly ILogger<GetCustomersFunction> _logger;
        private readonly TableStorageService _tableStorageService;

        public GetCustomersFunction(ILogger<GetCustomersFunction> logger, TableStorageService tableStorageService)
        {
            _logger = logger;
            _tableStorageService = tableStorageService;
        }

        [Function("GetCustomers")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequest req)
        {
            _logger.LogInformation("Getting all customers");

            try
            {
                var customers = await _tableStorageService.GetCustomersAsync();

                var customerDtos = customers.Select(c => new CustomerDto
                {
                    PartitionKey = c.PartitionKey,
                    RowKey = c.RowKey,
                    Timestamp = c.Timestamp,
                    ETag = c.ETag.ToString(),
                    Name = c.Name,
                    Surname = c.Surname,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    StreetAddress = c.StreetAddress,
                    City = c.City,
                    Province = c.Province,
                    PostalCode = c.PostalCode,
                    Country = c.Country,
                    CustomerPhotoUrl = c.CustomerPhotoUrl
                }).ToList();

                _logger.LogInformation($"Retrieved {customerDtos.Count} customers");
                return new OkObjectResult(customerDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving customers: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}