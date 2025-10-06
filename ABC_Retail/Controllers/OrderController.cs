using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ABC_Retail.Controllers
{
    public class OrderController : Controller
    {
        private readonly FunctionService _functionService;

        public OrderController(FunctionService functionService)
        {
            _functionService = functionService;
        }

        // Get all orders
        public async Task<IActionResult> Index()
        {
            try
            {
                var orders = await _functionService.GetOrdersAsync();
                return View(orders);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load orders: " + ex.Message;
                return View(new List<Order>());
            }
        }

        // Get order details
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var order = await _functionService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    return NotFound();
                }

                // Get customer and product details for display
                var customer = await _functionService.GetCustomerAsync(order.CustomerPartitionKey, order.CustomerRowKey);
                var product = await _functionService.GetProductAsync(order.ProductPartitionKey, order.ProductRowKey);

                ViewBag.Customer = customer;
                ViewBag.Product = product;

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load order: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Create order - GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            try
            {
                await PopulateDropdowns();
                return View(new Order { OrderDate = DateTime.Now });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading form: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Create order - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string CustomerPartitionKey, string ProductPartitionKey, int Quantity, DateTime OrderDate)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(CustomerPartitionKey) || string.IsNullOrEmpty(ProductPartitionKey))
                {
                    ModelState.AddModelError("", "Please select both customer and product.");
                    await PopulateDropdowns();
                    return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
                }

                if (Quantity <= 0)
                {
                    ModelState.AddModelError("", "Quantity must be greater than zero.");
                    await PopulateDropdowns();
                    return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
                }

                // Parse customer and product keys
                var customerKeys = CustomerPartitionKey.Split('|');
                var productKeys = ProductPartitionKey.Split('|');

                if (customerKeys.Length != 2 || productKeys.Length != 2)
                {
                    ModelState.AddModelError("", "Invalid selection format.");
                    await PopulateDropdowns();
                    return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
                }

                // Get product to calculate total amount
                var product = await _functionService.GetProductAsync(productKeys[0], productKeys[1]);
                if (product == null)
                {
                    ModelState.AddModelError("", "Selected product not found.");
                    await PopulateDropdowns();
                    return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
                }

                // Calculate total amount
                decimal totalAmount = (decimal)product.Price * Quantity;

                // Create order object
                var order = new Order
                {
                    CustomerPartitionKey = customerKeys[0],
                    CustomerRowKey = customerKeys[1],
                    ProductPartitionKey = productKeys[0],
                    ProductRowKey = productKeys[1],
                    Quantity = Quantity,
                    OrderDate = OrderDate,
                    TotalAmount = totalAmount,
                    OrderStatus = "Pending"
                };

                // Create order via Azure Function
                var success = await _functionService.CreateOrderAsync(order);

                if (success)
                {
                    TempData["Success"] = "Order created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "Failed to create order.";
                    await PopulateDropdowns();
                    return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Failed to create order: {ex.Message}");
                await PopulateDropdowns();
                return View(new Order { Quantity = Quantity, OrderDate = OrderDate });
            }
        }
        // Edit order - GET
        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var order = await _functionService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    return NotFound();
                }

                await PopulateDropdowns();
                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load order: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Edit order - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order order)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(order);
            }

            try
            {
                // Update order via Azure Function
                await _functionService.UpdateOrderAsync(order.PartitionKey, order.RowKey, order);

                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to update order: " + ex.Message);
                await PopulateDropdowns();
                return View(order);
            }
        }

        // Delete order - GET
        [HttpGet]
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
            {
                return NotFound();
            }

            try
            {
                var order = await _functionService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    return NotFound();
                }

                // Get customer and product details for display
                var customer = await _functionService.GetCustomerAsync(order.CustomerPartitionKey, order.CustomerRowKey);
                var product = await _functionService.GetProductAsync(order.ProductPartitionKey, order.ProductRowKey);

                ViewBag.Customer = customer;
                ViewBag.Product = product;

                return View(order);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load order: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Delete order - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            try
            {
                var result = await _functionService.DeleteOrderAsync(partitionKey, rowKey);

                if (!result)
                {
                    TempData["Error"] = "Order not found.";
                }
                else
                {
                    TempData["Success"] = "Order deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete order: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper method to populate dropdowns
        private async Task PopulateDropdowns()
        {
            try
            {
                // Get all customers for dropdown
                var customers = await _functionService.GetCustomersAsync();
                ViewBag.CustomerList = new SelectList(
                    customers.Select(c => new
                    {
                        Value = $"{c.PartitionKey}|{c.RowKey}",
                        Text = $"{c.Name} {c.Surname} ({c.Email})"
                    }),
                    "Value", "Text"
                );

                // Get all products for dropdown
                var products = await _functionService.GetProductsAsync();
                ViewBag.ProductList = new SelectList(
                    products.Select(p => new
                    {
                        Value = $"{p.PartitionKey}|{p.RowKey}",
                        Text = $"{p.ProductName} - R{p.Price:F2}"
                    }),
                    "Value", "Text"
                );
            }
            catch (Exception ex)
            {
                ViewBag.CustomerList = new SelectList(new List<object>());
                ViewBag.ProductList = new SelectList(new List<object>());
                ModelState.AddModelError(string.Empty, "Error loading dropdown data: " + ex.Message);
            }
        }
    }
}