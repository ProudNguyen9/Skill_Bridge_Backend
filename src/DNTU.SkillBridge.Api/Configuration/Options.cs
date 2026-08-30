using System.ComponentModel.DataAnnotations;
using DNTU.SkillBridge.Domain.Catalog;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Api.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required]
    public string Name { get; init; } = "DNTU SkillBridge";

    [Required, Url]
    public string FrontendBaseUrl { get; init; } = string.Empty;

    [Required, Url]
    public string PublicBaseUrl { get; init; } = string.Empty;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required, MinLength(64)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [MinLength(1)]
    public string[] AllowedOrigins { get; init; } = [];
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public bool Enabled { get; init; }

    /// <summary>Maximum lifetime of a client upload authorization.</summary>
    [Range(1, 60)] public int UploadUrlMinutes { get; init; } = 15;

    /// <summary>Maximum lifetime of a signed download URL.</summary>
    [Range(1, 15)] public int DownloadUrlMinutes { get; init; } = 5;

    [Required]
    public string Provider { get; init; } = "MinIO";

    public string? Endpoint { get; init; }

    public string? Bucket { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public bool UseSsl { get; init; }

    /// <summary>AWS region used by the Signature V4 S3-compatible adapter.</summary>
    [Required]
    public string Region { get; init; } = "us-east-1";

    /// <summary>Use /bucket/object addressing, which is required by the default MinIO endpoint.</summary>
    public bool UsePathStyle { get; init; } = true;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }

    public string? Host { get; init; }

    [Range(1, 65535)]
    public int Port { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    [EmailAddress]
    public string? FromAddress { get; init; }

    public string? FromName { get; init; }
}

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public bool Enabled { get; init; }

    [Required]
    public string Currency { get; init; } = "VND";

    [Range(1, 1440)]
    public int CheckoutExpirationMinutes { get; init; } = 15;

    [Range(1, 1440)]
    public int ReconciliationIntervalMinutes { get; init; } = 15;
}

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    public bool Enabled { get; init; }

    [Required]
    public string Environment { get; init; } = "Sandbox";

    public SePayPaymentGatewayOptions PaymentGateway { get; init; } = new();
}

public sealed class SePayPaymentGatewayOptions
{
    public string? ApiBaseUrl { get; init; }
    public string? CheckoutUrl { get; init; }
    public string? MerchantId { get; init; }
    public string? SecretKey { get; init; }
    public string? IpnSecretKey { get; init; }
    public string? SuccessUrl { get; init; }
    public string? ErrorUrl { get; init; }
    public string? CancelUrl { get; init; }
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; init; }
    public string? ConnectionString { get; init; }
}

public sealed class QuartzOptions
{
    public const string SectionName = "Quartz";

    public bool Enabled { get; init; }

    [Range(1, 1440)]
    public int FileCleanupIntervalMinutes { get; init; } = 15;
}

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public CatalogProjectMetadataOptions Project { get; init; } = new();

    public CatalogTaskMetadataOptions Task { get; init; } = new();
}

public sealed class CatalogProjectMetadataOptions
{
    // Array defaults stay empty: the configuration binder appends to non-empty defaults,
    // which would duplicate every configured value.
    public string[] Difficulties { get; init; } = [];

    public string[] WorkTypes { get; init; } = [];

    public CatalogRangeOptions DurationWeeks { get; init; } = new(4, 52);

    public CatalogRangeOptions TeamSize { get; init; } = new(2, 8);

    public CatalogAllowanceOptions Allowance { get; init; } = new();
}

public sealed class CatalogTaskMetadataOptions
{
    public string[] Statuses { get; init; } = [];

    public string[] Priorities { get; init; } = [];
}

public sealed record CatalogRangeOptions(int Min, int Max);

public sealed class CatalogAllowanceOptions
{
    public string Currency { get; init; } = "VND";

    public CatalogRangeOptions Amount { get; init; } = new(0, 100_000_000);
}

public sealed class AppOptionsValidator : IValidateOptions<AppOptions>, IValidateOptions<JwtOptions>, IValidateOptions<CorsOptions>, IValidateOptions<StorageOptions>, IValidateOptions<EmailOptions>, IValidateOptions<PaymentOptions>, IValidateOptions<SePayOptions>, IValidateOptions<RedisOptions>, IValidateOptions<CatalogOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options) =>
        ValidatePlaceholderValues(options, [options.FrontendBaseUrl, options.PublicBaseUrl])
        ?? ValidateUris(options.FrontendBaseUrl, options.PublicBaseUrl)
        ?? ValidateOptionsResult.Success;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 64 || IsPlaceholder(options.SigningKey))
        {
            return ValidateOptionsResult.Fail("Jwt:SigningKey must be a non-placeholder value of at least 64 characters.");
        }

        return ValidateOptionsResult.Success;
    }

    public ValidateOptionsResult Validate(string? name, CorsOptions options) =>
        options.AllowedOrigins.Length == 0 || options.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            ? ValidateOptionsResult.Fail("Cors:AllowedOrigins must contain absolute HTTP or HTTPS origins.")
            : ValidateOptionsResult.Success;

    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;

        var errors = new List<string>();
        var required = Require("Storage", options.Endpoint, options.Bucket, options.AccessKey, options.SecretKey, options.Region);
        if (required.Failed) errors.AddRange(required.Failures!);
        var provider = IsSupportedStorageProvider(options.Provider);
        if (provider.Failed) errors.AddRange(provider.Failures!);
        var endpoint = IsStorageEndpoint(options.Endpoint);
        if (endpoint.Failed) errors.AddRange(endpoint.Failures!);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public ValidateOptionsResult Validate(string? name, EmailOptions options) =>
        !options.Enabled
            ? ValidateOptionsResult.Success
            : Require("Email", options.Host, options.FromAddress, options.FromName);

    public ValidateOptionsResult Validate(string? name, PaymentOptions options) =>
        !options.Enabled || string.Equals(options.Currency, "VND", StringComparison.OrdinalIgnoreCase)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Payments:Currency must be VND for the initial payment integration.");

    public ValidateOptionsResult Validate(string? name, SePayOptions options) =>
        !options.Enabled
            ? ValidateOptionsResult.Success
            : Require("SePay:PaymentGateway", options.PaymentGateway.ApiBaseUrl, options.PaymentGateway.CheckoutUrl, options.PaymentGateway.MerchantId, options.PaymentGateway.SecretKey, options.PaymentGateway.IpnSecretKey);

    public ValidateOptionsResult Validate(string? name, RedisOptions options) =>
        !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Redis:ConnectionString is required when Redis is enabled.");

    public ValidateOptionsResult Validate(string? name, CatalogOptions options)
    {
        var errors = new List<string>();

        ValidateEnumValues<ProjectDifficulty>(options.Project.Difficulties, "Catalog:Project:Difficulties", errors);
        ValidateEnumValues<ProjectWorkType>(options.Project.WorkTypes, "Catalog:Project:WorkTypes", errors);
        ValidateEnumValues<ProjectTaskStatus>(options.Task.Statuses, "Catalog:Task:Statuses", errors);
        ValidateEnumValues<ProjectTaskPriority>(options.Task.Priorities, "Catalog:Task:Priorities", errors);
        ValidateRange(options.Project.DurationWeeks, "Catalog:Project:DurationWeeks", errors);
        ValidateRange(options.Project.TeamSize, "Catalog:Project:TeamSize", errors);
        ValidateRange(options.Project.Allowance.Amount, "Catalog:Project:Allowance:Amount", errors);

        if (string.IsNullOrWhiteSpace(options.Project.Allowance.Currency))
        {
            errors.Add("Catalog:Project:Allowance:Currency is required.");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    private static void ValidateEnumValues<TEnum>(IEnumerable<string> values, string settingName, List<string> errors) where TEnum : struct, Enum
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            errors.Add($"{settingName} must contain at least one value.");
            return;
        }

        var invalid = list.Where(value => !Enum.TryParse<TEnum>(value, true, out _)).ToList();
        if (invalid.Count > 0)
        {
            errors.Add($"{settingName} contains values that are not defined: {string.Join(", ", invalid)}.");
        }
    }

    private static void ValidateRange(CatalogRangeOptions range, string settingName, List<string> errors)
    {
        if (range.Min > range.Max)
        {
            errors.Add($"{settingName} min must be less than or equal to max.");
        }
    }

    private static ValidateOptionsResult? ValidatePlaceholderValues(AppOptions options, IEnumerable<string?> values) =>
        values.Any(value => string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
            ? ValidateOptionsResult.Fail("Application URL settings must be configured with non-placeholder values.")
            : null;

    private static ValidateOptionsResult? ValidateUris(params string[] values) =>
        values.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            ? ValidateOptionsResult.Fail("Application URL settings must be absolute HTTP or HTTPS URIs.")
            : null;

    private static ValidateOptionsResult Require(string section, params string?[] values) =>
        values.Any(value => string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
            ? ValidateOptionsResult.Fail($"{section} contains a required missing or placeholder configuration value.")
            : ValidateOptionsResult.Success;

    private static ValidateOptionsResult IsSupportedStorageProvider(string provider) =>
        string.Equals(provider, "MinIO", StringComparison.OrdinalIgnoreCase) || string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Storage:Provider must be MinIO or S3.");

    private static ValidateOptionsResult IsStorageEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Storage:Endpoint must be an absolute HTTP or HTTPS URI.");

    private static bool IsPlaceholder(string value) => value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
