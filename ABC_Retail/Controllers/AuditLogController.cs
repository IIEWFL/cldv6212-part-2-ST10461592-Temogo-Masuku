using ABC_Retail.Models;
using ABC_Retail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABC_Retail.Controllers
{
    public class AuditLogController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        // GET: AuditLog
        public async Task<IActionResult> Index()
        {
            try
            {
                var auditLogs = await _auditLogService.GetAllAuditLogsAsync();
                return View(auditLogs);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load audit logs: " + ex.Message;
                return View(new List<AuditLog>());
            }
        }

        // GET: AuditLog/Details/5
        public async Task<IActionResult> Details(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return NotFound();
            }

            try
            {
                var auditLogs = await _auditLogService.GetAllAuditLogsAsync();
                var auditLog = auditLogs.FirstOrDefault(log => log.MessageId == messageId);

                if (auditLog == null)
                {
                    return NotFound();
                }

                return View(auditLog);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load audit log: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: AuditLog/Export
        public async Task<IActionResult> Export(string format = "json")
        {
            try
            {
                var auditLogs = await _auditLogService.GetAllAuditLogsAsync();

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateCsv(auditLogs);
                    return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "audit_logs.csv");
                }
                else
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(auditLogs, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "audit_logs.json");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to export audit logs: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private string GenerateCsv(IEnumerable<AuditLog> auditLogs)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("MessageId,InsertionTime,MessageText");

            foreach (var log in auditLogs)
            {
                csv.AppendLine($"\"{log.MessageId}\",\"{log.InsertionTime}\",\"{log.MessageText?.Replace("\"", "\"\"")}\"");
            }

            return csv.ToString();
        }
    }
}