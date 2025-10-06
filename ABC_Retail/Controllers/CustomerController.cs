using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ABC_Retail.Controllers
{
    public class CustomerController : Controller
    {
        private readonly FunctionService _functionService;

        public CustomerController(FunctionService functionService)
        {
            _functionService = functionService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var customers = await _functionService.GetCustomersAsync();
                return View(customers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load customers: " + ex.Message;
                return View(new List<Customer>());
            }
        }

        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var customer = await _functionService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    return NotFound();
                }
                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load customer: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, IFormFile photoFile)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            try
            {
                // Validate photo if provided
                if (photoFile != null && photoFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(photoFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("photoFile", "Please upload a valid image file.");
                        return View(customer);
                    }
                }

                // Call the function service
                var success = await _functionService.CreateCustomerAsync(customer, photoFile);

                if (success)
                {
                    TempData["Success"] = "Customer created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "Failed to create customer.";
                    return View(customer);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to create customer: " + ex.Message;
                return View(customer);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var customer = await _functionService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    return NotFound();
                }
                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load customer: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer, IFormFile photoFile)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            try
            {
                Stream? photoStream = null;
                string? photoFileName = null;

                if (photoFile != null && photoFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(photoFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("photoFile", "Please upload a valid image file.");
                        return View(customer);
                    }

                    photoStream = photoFile.OpenReadStream();
                    photoFileName = photoFile.FileName;
                }

                await _functionService.UpdateCustomerAsync(customer.PartitionKey, customer.RowKey, customer, photoStream, photoFileName);

                TempData["Success"] = "Customer updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to update customer: " + ex.Message;
                return View(customer);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var customer = await _functionService.GetCustomerAsync(partitionKey, rowKey);
                if (customer == null)
                {
                    return NotFound();
                }
                return View(customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load customer: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            try
            {
                var result = await _functionService.DeleteCustomerAsync(partitionKey, rowKey);

                if (!result)
                {
                    TempData["Error"] = "Customer not found.";
                }
                else
                {
                    TempData["Success"] = "Customer deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete customer: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}