using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace ABC_Retail.Models
{
    public class Product : ITableEntity
    {
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [Required]
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Price { get; set; }
        public string? ProductPhotoUrl { get; set; }  // Product image
        public string? Category { get; set; }
    }
}
