#nullable enable
using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Models
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
        public string? ProductPhotoUrl { get; set; }  
        public string? Category { get; set; }
    }
}
