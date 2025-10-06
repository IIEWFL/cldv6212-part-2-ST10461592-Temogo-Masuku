#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Models
{
    public class AuditLog
    {
        public string? MessageId { get; set; }
        public DateTimeOffset? InsertionTime { get; set; }
        public string MessageText { get; internal set; } = string.Empty;
    }
}
