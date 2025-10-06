using ABC_Retail.Models;
using Azure;
using Azure.Data.Tables;

namespace ABC_Retail.Services.Storage
{
    public class TableStorageService
    {
        private readonly TableClient _customersTable;
        private readonly TableClient _productsTable;
        private readonly TableClient _ordersTable;

        public TableStorageService(string storageConnectionString)
        {
            var serviceClient = new TableServiceClient(storageConnectionString);

            // Create separate table clients for each entity type
            _customersTable = serviceClient.GetTableClient("customers");
            _productsTable = serviceClient.GetTableClient("products");
            _ordersTable = serviceClient.GetTableClient("orders");

            // Create tables if they don't exist
            _customersTable.CreateIfNotExists();
            _productsTable.CreateIfNotExists();
            _ordersTable.CreateIfNotExists();
        }

        #region Customer Operations
        public async Task<List<Customer>> GetCustomersAsync()
        {
            var customers = new List<Customer>();
            await foreach (var customer in _customersTable.QueryAsync<Customer>())
            {
                customers.Add(customer);
            }
            return customers;
        }

        public async Task<Customer?> GetCustomerAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _customersTable.GetEntityAsync<Customer>(partitionKey, rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task InsertCustomerAsync(Customer customer)
        {
            customer.PartitionKey = customer.Province ?? "Unknown";
            customer.RowKey = Guid.NewGuid().ToString();
            await _customersTable.AddEntityAsync(customer);
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            await _customersTable.UpdateEntityAsync(customer, ETag.All, TableUpdateMode.Replace);
        }

        public async Task DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            await _customersTable.DeleteEntityAsync(partitionKey, rowKey);
        }
        #endregion

        #region Product Operations
        public async Task<List<Product>> GetProductsAsync()
        {
            var products = new List<Product>();
            await foreach (var product in _productsTable.QueryAsync<Product>())
            {
                products.Add(product);
            }
            return products;
        }

        public async Task<Product?> GetProductAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _productsTable.GetEntityAsync<Product>(partitionKey, rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task InsertProductAsync(Product product)
        {
            product.PartitionKey = product.Category ?? "General";
            product.RowKey = Guid.NewGuid().ToString();
            await _productsTable.AddEntityAsync(product);
        }

        public async Task UpdateProductAsync(Product product)
        {
            await _productsTable.UpdateEntityAsync(product, ETag.All, TableUpdateMode.Replace);
        }

        public async Task DeleteProductAsync(string partitionKey, string rowKey)
        {
            await _productsTable.DeleteEntityAsync(partitionKey, rowKey);
        }
        #endregion

        #region Order Operations
        public async Task<List<Order>> GetOrdersAsync()
        {
            var orders = new List<Order>();
            await foreach (var order in _ordersTable.QueryAsync<Order>())
            {
                orders.Add(order);
            }
            return orders;
        }

        public async Task<Order?> GetOrderAsync(string partitionKey, string rowKey)
        {
            try
            {
                var response = await _ordersTable.GetEntityAsync<Order>(partitionKey, rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task InsertOrderAsync(Order order)
        {
            order.PartitionKey = order.OrderDate.ToString("yyyy-MM");
            order.RowKey = Guid.NewGuid().ToString();
            await _ordersTable.AddEntityAsync(order);
        }

        public async Task UpdateOrderAsync(Order order)
        {
            await _ordersTable.UpdateEntityAsync(order, ETag.All, TableUpdateMode.Replace);
        }

        public async Task DeleteOrderAsync(string partitionKey, string rowKey)
        {
            await _ordersTable.DeleteEntityAsync(partitionKey, rowKey);
        }
        #endregion
    }
}