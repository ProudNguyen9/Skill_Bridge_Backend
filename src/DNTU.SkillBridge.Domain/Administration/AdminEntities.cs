using DNTU.SkillBridge.Domain.Common;

namespace DNTU.SkillBridge.Domain.Administration;

public static class AuditActions
{
    public const string PaymentReceived = "PAYMENT_RECEIVED";
    public const string DisbursementPaid = "DISBURSEMENT_PAID";
    public const string PolicyUpdated = "POLICY_UPDATED";
    public const string ReportExportRequested = "REPORT_EXPORT_REQUESTED";
}

public sealed class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public Guid? CompanyId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpHash { get; private set; }
    public string? UserAgent { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog() { }

    public AuditLog(Guid? actorUserId, string action, string entityType, string entityId, Guid? companyId, Guid? projectId, string? correlationId, string? ipHash, string? userAgent, string metadataJson, DateTimeOffset createdAt)
    {
        ActorUserId = actorUserId;
        Action = Required(action, nameof(action), 100);
        EntityType = Required(entityType, nameof(entityType), 100);
        EntityId = Required(entityId, nameof(entityId), 100);
        CompanyId = companyId;
        ProjectId = projectId;
        CorrelationId = Trim(correlationId, 100);
        IpHash = Trim(ipHash, 128);
        UserAgent = Trim(userAgent, 500);
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        CreatedAt = createdAt;
    }

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(maxLength, value.Trim().Length)];
}

public sealed class AdminPolicySetting : AuditableEntity
{
    public string Category { get; private set; } = string.Empty;
    public string SettingsJson { get; private set; } = "{}";
    public int Version { get; private set; } = 1;

    private AdminPolicySetting() { }

    public AdminPolicySetting(string category, string settingsJson)
    {
        Category = Required(category, nameof(category), 80);
        SettingsJson = string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson;
    }

    public void Update(string settingsJson)
    {
        SettingsJson = string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson;
        Version++;
    }

    private static string Required(string value, string name, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength ? value.Trim() : throw new ArgumentException($"{name} is required.", name);
}
