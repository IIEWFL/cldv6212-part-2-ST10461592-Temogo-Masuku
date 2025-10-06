namespace ABC_Retail.Models
{
    public class AuditLog
    {
        public string? MessageId { get; set; }
        public DateTimeOffset? InsertionTime { get; set; }
        public string MessageText { get; internal set; } = string.Empty;
    }
}
