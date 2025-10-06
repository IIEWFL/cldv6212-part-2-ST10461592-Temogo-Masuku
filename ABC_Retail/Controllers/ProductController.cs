using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ABC_Retail.Controllers
{
    public class ProductController : Controller
    {
        private readonly FunctionService _functionService;

        public ProductController(FunctionService functionService)
        {
            _functionService = functionService;
        }

        // Get all products
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _functionService.GetProductsAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load products: " + ex.Message;
                return View(new List<Product>());
            }
        }

        // Get product details
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var product = await _functionService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    return NotFound();
                }
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Create product - GET
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Product());
        }

        // Create product - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile photoFile)
        {
            if (!ModelState.IsValid)
            {
                return View(product);
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
                        return View(product);
                    }
                }

                // Call the function service
                var success = await _functionService.CreateProductAsync(product, photoFile);

                if (success)
                {
                    TempData["Success"] = "Product created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "Failed to create product.";
                    return View(product);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to create product: " + ex.Message;
                return View(product);
            }
        }

        // Edit product - GET
        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var product = await _functionService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    return NotFound();
                }
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Edit product - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile photoFile)
        {
            if (!ModelState.IsValid)
            {
                return View(product);
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
                        return View(product);
                    }

                    photoStream = photoFile.OpenReadStream();
                    photoFileName = photoFile.FileName;
                }

                await _functionService.UpdateProductAsync(product.PartitionKey, product.RowKey, product, photoStream, photoFileName);

                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to update product: " + ex.Message;
                return View(product);
            }
        }

        // Delete product - GET
        [HttpGet]
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var product = await _functionService.GetProductAsync(partitionKey, rowKey);
                if (product == null)
                {
                    return NotFound();
                }
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Delete product - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            try
            {
                var result = await _functionService.DeleteProductAsync(partitionKey, rowKey);

                if (!result)
                {
                    TempData["Error"] = "Product not found.";
                }
                else
                {
                    TempData["Success"] = "Product deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete product: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}