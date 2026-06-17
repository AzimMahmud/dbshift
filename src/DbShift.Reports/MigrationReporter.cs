using DbShift.Core.Entities;
using DbShift.Core.Enums;

namespace DbShift.Reports;

public static class MigrationReporter
{
    public static string GenerateStatusReport(IReadOnlyList<MigrationRecord> records, string environment)
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine($"Migration Status Report - {environment}");
        report.AppendLine(new string('=', 60));
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        
        var completed = records.Count(m => m.Status == MigrationStatus.Completed);
        var failed = records.Count(m => m.Status == MigrationStatus.Failed);
        var pending = records.Count(m => m.Status == MigrationStatus.Pending);
        
        report.AppendLine($"Total Migrations: {records.Count}");
        report.AppendLine($"Completed: {completed}");
        report.AppendLine($"Failed: {failed}");
        report.AppendLine($"Pending: {pending}");
        report.AppendLine();
        
        if (records.Any())
        {
            report.AppendLine("Migration Details:");
            report.AppendLine(new string('-', 60));
            
            foreach (var record in records.OrderBy(r => r.Version))
            {
                var statusIcon = record.Status switch
                {
                    MigrationStatus.Completed => "[OK]",
                    MigrationStatus.Failed => "[FAIL]",
                    MigrationStatus.Pending => "[PENDING]",
                    MigrationStatus.InProgress => "[IN PROGRESS]",
                    MigrationStatus.RolledBack => "[ROLLED BACK]",
                    _ => "[UNKNOWN]"
                };
                
                report.AppendLine($"  {statusIcon} {record.Version}: {record.Name}");
                report.AppendLine($"       Type: {record.Type} | Executed: {record.ExecutedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            }
        }
        
        return report.ToString();
    }
    
    public static string GenerateHistoryReport(IReadOnlyList<MigrationAuditEntry> entries, string environment)
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine($"Migration Audit History - {environment}");
        report.AppendLine(new string('=', 60));
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        
        if (!entries.Any())
        {
            report.AppendLine("No audit entries found.");
            return report.ToString();
        }
        
        report.AppendLine("Audit Entries:");
        report.AppendLine(new string('-', 60));
        
        foreach (var entry in entries.OrderByDescending(e => e.PerformedAtUtc))
        {
            report.AppendLine($"  [{entry.Action}] {entry.PerformedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine($"       By: {entry.PerformedBy}");
            if (!string.IsNullOrEmpty(entry.Details))
            {
                report.AppendLine($"       Details: {entry.Details}");
            }
        }
        
        return report.ToString();
    }
}
