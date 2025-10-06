using ABC_Retail.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ABC_Retail.Controllers
{
    public class ContractController : Controller
    {
        private readonly FileShareStorageService _fileShareService;
        private readonly QueueStorageService _queueStorageService;

        public ContractController(
            FileShareStorageService fileShareService,
            QueueStorageService queueStorageService)
        {
            _fileShareService = fileShareService;
            _queueStorageService = queueStorageService;
        }

        // GET: Contract
        public async Task<IActionResult> Index()
        {
            try
            {
                var files = await _fileShareService.ListFilesAsync();

                // Create a list of file info with icons
                var fileInfoList = files.Select(f => new
                {
                    FileName = f,
                    Extension = Path.GetExtension(f).ToLowerInvariant().TrimStart('.'),
                    Icon = GetFileIcon(f)
                }).ToList();

                return View(fileInfoList);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load contracts: " + ex.Message;
                return View(new List<dynamic>());
            }
        }

        // GET: Contract/Upload
        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Contract/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile contractFile)
        {
            if (contractFile == null || contractFile.Length == 0)
            {
                ModelState.AddModelError("contractFile", "Please select a file to upload.");
                return View();
            }

            try
            {
                // Validate file type (allow PDF, DOCX, TXT, etc.)
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".txt" };
                var fileExtension = Path.GetExtension(contractFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("contractFile", "Please upload a valid contract file (pdf, docx, doc, txt).");
                    return View();
                }

                // Validate file size (max 10MB)
                if (contractFile.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("contractFile", "File size cannot exceed 10MB.");
                    return View();
                }

                using var stream = contractFile.OpenReadStream();
                await _fileShareService.UploadFileAsync(contractFile.FileName, stream);

                // Log the upload
                var logEntry = new
                {
                    Action = "Contract Uploaded",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Contract",
                    Details = new
                    {
                        FileName = contractFile.FileName,
                        FileSize = contractFile.Length,
                        FileType = fileExtension
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);

                TempData["Success"] = $"Contract '{contractFile.FileName}' uploaded successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to upload contract: " + ex.Message;
                return View();
            }
        }

        // GET: Contract/Download/filename
        public async Task<IActionResult> Download(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return NotFound();
            }

            try
            {
                var fileStream = await _fileShareService.DownloadFileAsync(fileName);
                var contentType = GetContentType(fileName);

                // Log the download
                var logEntry = new
                {
                    Action = "Contract Downloaded",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Contract",
                    Details = new
                    {
                        FileName = fileName
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);

                return File(fileStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to download contract: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Contract/Delete/filename
        [HttpGet]
        public IActionResult Delete(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return NotFound();
            }

            ViewBag.FileName = fileName;
            return View();
        }

        // POST: Contract/Delete/filename
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string fileName)
        {
            try
            {
                var result = await _fileShareService.DeleteFileAsync(fileName);

                if (!result)
                {
                    TempData["Error"] = "Contract not found.";
                }
                else
                {
                    // Log the deletion
                    var logEntry = new
                    {
                        Action = "Contract Deleted",
                        Timestamp = DateTime.UtcNow,
                        EntityType = "Contract",
                        Details = new
                        {
                            FileName = fileName
                        }
                    };
                    await _queueStorageService.SendLogEntryAsync(logEntry);

                    TempData["Success"] = $"Contract '{fileName}' deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete contract: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper method to get file icon
        private string GetFileIcon(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (extension == ".pdf")
                return "bi-file-pdf";
            else if (extension == ".doc" || extension == ".docx")
                return "bi-file-word";
            else if (extension == ".txt")
                return "bi-file-text";
            else
                return "bi-file-earmark";
        }

        // Helper method to determine content type
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}