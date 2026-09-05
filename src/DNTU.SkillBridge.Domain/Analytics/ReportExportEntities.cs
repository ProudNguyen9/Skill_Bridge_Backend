using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Analytics;

public sealed class ReportExport : AuditableEntity
{
    public Guid RequestedByUserId { get; private set; }
    public string ReportType { get; private set; } = string.Empty;
    public string Format { get; private set; } = "CSV";
    public string FilterJson { get; private set; } = "{}";
    public ReportExportStatus Status { get; private set; } = ReportExportStatus.QUEUED;
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public string? Content { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private ReportExport() { }

    public ReportExport(Guid requestedByUserId, string reportType, string format, string filterJson, DateTimeOffset expiresAt)
    {
        RequestedByUserId = requestedByUserId;
        ReportType = Required(reportType, nameof(reportType), 60).ToUpperInvariant();
        Format = Required(format, nameof(format), 10).ToUpperInvariant();
        FilterJson = string.IsNullOrWhiteSpace(filterJson) ? "{}" : filterJson;
        ExpiresAt = expiresAt;
    }

    public void Complete(string fileName, string contentType, string content, DateTimeOffset completedAt)
    {
        FileName = Required(fileName, nameof(fileName), 200);
        ContentType = Required(contentType, nameof(contentType), 100);
        Content = content;
        CompletedAt = completedAt;
        Status = ReportExportStatus.COMPLETED;
    }

    public void Fail(string safeReason)
    {
        FailureReason = string.IsNullOrWhiteSpace(safeReason) ? "Export failed." : safeReason.Trim()[..Math.Min(300, safeReason.Trim().Length)];
        Status = ReportExportStatus.FAILED;
    }

    public bool IsDownloadable(DateTimeOffset now) => Status == ReportExportStatus.COMPLETED && ExpiresAt > now;

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}
