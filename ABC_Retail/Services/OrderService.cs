using ABC_Retail.Models;
using ABC_Retail.Services.Storage;
using System.ComponentModel.DataAnnotations;

namespace ABC_Retail.Services
{
    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    public interface IOrderService
    {
        Task<IEnumerable<Order>> GetAllOrdersAsync();
        Task<Order?> GetOrderByIdAsync(string partitionKey, string rowKey);
        Task<Order> CreateOrderAsync(Order order);
        Task<Order> UpdateOrderAsync(Order order);
        Task<Order> UpdateOrderStatusAsync(string partitionKey, string rowKey, OrderStatus status);
        Task<bool> DeleteOrderAsync(string partitionKey, string rowKey);
        Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customerPartitionKey, string customerRowKey);
        Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<ValidationResult> ValidateOrderAsync(Order order);
    }

    public class OrderService : IOrderService
    {
        private readonly TableStorageService _tableStorageService;
        private readonly QueueStorageService _queueStorageService;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;

        public OrderService(
            TableStorageService tableStorageService,
            QueueStorageService queueStorageService,
            ICustomerService customerService,
            IProductService productService)
        {
            _tableStorageService = tableStorageService;
            _queueStorageService = queueStorageService;
            _customerService = customerService;
            _productService = productService;
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            try
            {
                return await _tableStorageService.GetOrdersAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync("GetAllOrders", ex);
                throw new InvalidOperationException("Failed to retrieve orders", ex);
            }
        }

        public async Task<Order?> GetOrderByIdAsync(string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(partitionKey) || string.IsNullOrEmpty(rowKey))
                return null;

            try
            {
                return await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetOrderById - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to retrieve order", ex);
            }
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            // Validate order
            var validationResult = await ValidateOrderAsync(order);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            try
            {
                // Verify customer exists
                var customer = await _customerService.GetCustomerByIdAsync(order.CustomerPartitionKey, order.CustomerRowKey);
                if (customer == null)
                {
                    throw new ArgumentException("Customer not found");
                }

                // Verify product exists and calculate total
                var product = await _productService.GetProductByIdAsync(order.ProductPartitionKey, order.ProductRowKey);
                if (product == null)
                {
                    throw new ArgumentException("Product not found");
                }

                // Set default values
                order.PartitionKey = order.OrderDate.ToString("yyyy-MM");
                order.RowKey = Guid.NewGuid().ToString();
                order.TotalAmount = (decimal)product.Price * order.Quantity;
                order.OrderStatus = OrderStatus.Pending.ToString();

                await _tableStorageService.InsertOrderAsync(order);

                // Log the creation
                await LogOrderActionAsync("Order Created", order, customer, product);

                return order;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await LogErrorAsync("CreateOrder", ex);
                throw new InvalidOperationException("Failed to create order", ex);
            }
        }

        public async Task<Order> UpdateOrderAsync(Order order)
        {
            // Validate order
            var validationResult = await ValidateOrderAsync(order);
            if (validationResult != ValidationResult.Success)
            {
                throw new ArgumentException(validationResult.ErrorMessage);
            }

            try
            {
                // Recalculate total amount
                var product = await _productService.GetProductByIdAsync(order.ProductPartitionKey, order.ProductRowKey);
                if (product != null)
                {
                    order.TotalAmount = (decimal)product.Price * order.Quantity;
                }

                await _tableStorageService.UpdateOrderAsync(order);

                // Log the update
                var customer = await _customerService.GetCustomerByIdAsync(order.CustomerPartitionKey, order.CustomerRowKey);
                await LogOrderActionAsync("Order Updated", order, customer, product);

                return order;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await LogErrorAsync("UpdateOrder", ex);
                throw new InvalidOperationException("Failed to update order", ex);
            }
        }

        public async Task<Order> UpdateOrderStatusAsync(string partitionKey, string rowKey, OrderStatus status)
        {
            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                {
                    throw new ArgumentException("Order not found");
                }

                var oldStatus = order.OrderStatus;
                order.OrderStatus = status.ToString();

                await _tableStorageService.UpdateOrderAsync(order);

                // Log the status change
                var logEntry = new
                {
                    Action = "Order Status Updated",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Order",
                    Details = new
                    {
                        order.PartitionKey,
                        order.RowKey,
                        OldStatus = oldStatus,
                        NewStatus = status.ToString()
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);

                return order;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await LogErrorAsync($"UpdateOrderStatus - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to update order status", ex);
            }
        }

        public async Task<bool> DeleteOrderAsync(string partitionKey, string rowKey)
        {
            try
            {
                var order = await _tableStorageService.GetOrderAsync(partitionKey, rowKey);
                if (order == null)
                    return false;

                await _tableStorageService.DeleteOrderAsync(partitionKey, rowKey);

                // Log the deletion
                var logEntry = new
                {
                    Action = "Order Deleted",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Order",
                    Details = new
                    {
                        PartitionKey = partitionKey,
                        RowKey = rowKey,
                        OrderAmount = order.TotalAmount
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);

                return true;
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"DeleteOrder - {partitionKey}/{rowKey}", ex);
                throw new InvalidOperationException("Failed to delete order", ex);
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customerPartitionKey, string customerRowKey)
        {
            try
            {
                var allOrders = await _tableStorageService.GetOrdersAsync();
                return allOrders.Where(o => o.CustomerPartitionKey == customerPartitionKey &&
                                           o.CustomerRowKey == customerRowKey);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetOrdersByCustomer - {customerPartitionKey}/{customerRowKey}", ex);
                throw new InvalidOperationException("Failed to retrieve orders by customer", ex);
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
        {
            try
            {
                var allOrders = await _tableStorageService.GetOrdersAsync();
                return allOrders.Where(o => string.Equals(o.OrderStatus, status.ToString(), StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetOrdersByStatus - {status}", ex);
                throw new InvalidOperationException("Failed to retrieve orders by status", ex);
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var allOrders = await _tableStorageService.GetOrdersAsync();
                return allOrders.Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate);
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"GetOrdersByDateRange - {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}", ex);
                throw new InvalidOperationException("Failed to retrieve orders by date range", ex);
            }
        }

        public async Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var allOrders = await _tableStorageService.GetOrdersAsync();
                var filteredOrders = allOrders.Where(o =>
                    (!startDate.HasValue || o.OrderDate >= startDate.Value) &&
                    (!endDate.HasValue || o.OrderDate <= endDate.Value) &&
                    !string.Equals(o.OrderStatus, OrderStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase));

                return (decimal)filteredOrders.Sum(o => o.TotalAmount);
            }
            catch (Exception ex)
            {
                await LogErrorAsync("GetTotalSales", ex);
                throw new InvalidOperationException("Failed to calculate total sales", ex);
            }
        }

        public Task<ValidationResult> ValidateOrderAsync(Order order)
        {
            var context = new ValidationContext(order);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(order, context, results, true))
            {
                return Task.FromResult(results.First());
            }

            // Additional business rules
            if (order.Quantity <= 0)
            {
                return Task.FromResult(new ValidationResult("Quantity must be greater than zero"));
            }
            return Task.FromResult(ValidationResult.Success!);
        }

        private async Task LogOrderActionAsync(string action, Order order, Customer? customer = null, Product? product = null)
        {
            try
            {
                var logEntry = new
                {
                    Action = action,
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Order",
                    Details = new
                    {
                        order.PartitionKey,
                        order.RowKey,
                        CustomerName = customer != null ? $"{customer.Name} {customer.Surname}" : "Unknown",
                        ProductName = product?.ProductName ?? "Unknown",
                        order.Quantity,
                        order.TotalAmount,
                        order.OrderStatus
                    }
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don't fail the main operation if logging fails
            }
        }

        private async Task LogErrorAsync(string operation, Exception ex)
        {
            try
            {
                var logEntry = new
                {
                    Action = "Error",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "Order",
                    Operation = operation,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                };
                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch
            {
                // Don't fail the main operation if logging fails
            }
        }
    }
}