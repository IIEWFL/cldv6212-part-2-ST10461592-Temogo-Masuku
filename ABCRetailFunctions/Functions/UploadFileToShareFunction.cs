using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailFunctions.Functions
{
    public class UploadFileToShareFunction
    {
        private readonly ILogger<UploadFileToShareFunction> _logger;
        private readonly FileShareStorageService _fileShareStorageService;

        public UploadFileToShareFunction(ILogger<UploadFileToShareFunction> logger, FileShareStorageService fileShareStorageService)
        {
            _logger = logger;
            _fileShareStorageService = fileShareStorageService;
        }

        [Function("UploadFileToShare")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "fileshare/upload")] HttpRequest req)
        {
            _logger.LogInformation("Uploading file to File Share");

            try
            {
                if (!req.Form.Files.Any())
                {
                    return new BadRequestObjectResult("No file uploaded");
                }

                var file = req.Form.Files[0];

                using (var stream = file.OpenReadStream())
                {
                    await _fileShareStorageService.UploadFileAsync(file.FileName, stream);
                }

                _logger.LogInformation($"File uploaded successfully: {file.FileName}");

                return new OkObjectResult(new
                {
                    message = "File uploaded successfully",
                    fileName = file.FileName,
                    shareName = "retail-fileshare"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}