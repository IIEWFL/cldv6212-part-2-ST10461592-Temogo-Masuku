using ABC_Retail.Models;
using ABC_Retail.Services.Storage;

namespace ABC_Retail.Services
{
    public interface IAuditLogService
    {
        Task<IEnumerable<AuditLog>> GetAllAuditLogsAsync();
        Task SendLogEntryAsync(object message);
        Task<IEnumerable<AuditLog>> GetLogsByEntityTypeAsync(string entityType);
        Task<IEnumerable<AuditLog>> GetLogsByActionAsync(string action);
        Task<IEnumerable<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task ClearOldLogsAsync(int daysToKeep = 30);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly QueueStorageService _queueStorageService;

        public AuditLogService(QueueStorageService queueStorageService)
        {
            _queueStorageService = queueStorageService;
        }

        public async Task<IEnumerable<AuditLog>> GetAllAuditLogsAsync()
        {
            try
            {
                return await _queueStorageService.GetLogEntriesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to retrieve audit logs", ex);
            }
        }

        public async Task SendLogEntryAsync(object message)
        {
            try
            {
                await _queueStorageService.SendLogEntryAsync(message);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send log entry", ex);
            }
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByEntityTypeAsync(string entityType)
        {
            try
            {
                var allLogs = await _queueStorageService.GetLogEntriesAsync();
                return allLogs.Where(log =>
                    log.MessageText.Contains($"\"EntityType\":\"{entityType}\"", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve logs for entity type: {entityType}", ex);
            }
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByActionAsync(string action)
        {
            try
            {
                var allLogs = await _queueStorageService.GetLogEntriesAsync();
                return allLogs.Where(log =>
                    log.MessageText.Contains($"\"Action\":\"{action}\"", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve logs for action: {action}", ex);
            }
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var allLogs = await _queueStorageService.GetLogEntriesAsync();
                return allLogs.Where(log =>
                    log.InsertionTime.HasValue &&
                    log.InsertionTime.Value >= startDate &&
                    log.InsertionTime.Value <= endDate);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve logs for date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}", ex);
            }
        }

        public async Task ClearOldLogsAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var allLogs = await _queueStorageService.GetLogEntriesAsync();
                var oldLogs = allLogs.Where(log =>
                    log.InsertionTime.HasValue &&
                    log.InsertionTime.Value < cutoffDate);

                var oldLogCount = oldLogs.Count();

                // Log the cleanup operation
                var logEntry = new
                {
                    Action = "Audit Log Cleanup",
                    Timestamp = DateTime.UtcNow,
                    EntityType = "AuditLog",
                    Details = new
                    {
                        DaysToKeep = daysToKeep,
                        CutoffDate = cutoffDate,
                        OldLogsFound = oldLogCount
                    }
                };

                await _queueStorageService.SendLogEntryAsync(logEntry);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to clear old logs", ex);
            }
        }
    }
}

