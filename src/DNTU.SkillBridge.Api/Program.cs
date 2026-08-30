using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using DNTU.SkillBridge.Api.Authentication;

using DNTU.SkillBridge.Api.Realtime;
using DNTU.SkillBridge.Infrastructure.Realtime;
using DNTU.SkillBridge.Application.Catalog;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Application.Common.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DNTU.SkillBridge.Api.Security;
using DNTU.SkillBridge.Application.Common.Security;
using DNTU.SkillBridge.Infrastructure.Persistence.Seed;
using DNTU.SkillBridge.Api.Middleware;
using DNTU.SkillBridge.Infrastructure.Persistence;
using DNTU.SkillBridge.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AppOptions>()
    .BindConfiguration(AppOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CorsOptions>()
    .BindConfiguration(CorsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<StorageOptions>()
    .BindConfiguration(StorageOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailOptions>()
    .BindConfiguration(EmailOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration(PaymentOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SePayOptions>()
    .BindConfiguration(SePayOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RedisOptions>()
    .BindConfiguration(RedisOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
.AddOptions<QuartzOptions>()
.BindConfiguration(QuartzOptions.SectionName)
.ValidateDataAnnotations()
.ValidateOnStart();

builder.Services
    .AddOptions<CatalogOptions>()
    .BindConfiguration(CatalogOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<CorsOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<StorageOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<EmailOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PaymentOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<SePayOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<RedisOptions>, AppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<CatalogOptions>, AppOptionsValidator>();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgresql");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Students.StudentService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Companies.CompanyService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Lecturers.LecturerService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Projects.ProjectService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Projects.ProjectCompletionService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Students.SavedProjectService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Applications.ApplicationService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Teams.TeamService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Commitments.ICommitmentService, DNTU.SkillBridge.Application.Commitments.CommitmentService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Workspaces.IProjectAccessService, DNTU.SkillBridge.Application.Workspaces.ProjectAccessService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Workspaces.IProjectActivityWriter, DNTU.SkillBridge.Api.Workspaces.ProjectActivityWriter>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Workspaces.IWorkspaceService, DNTU.SkillBridge.Application.Workspaces.WorkspaceService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Workspaces.DashboardService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Workspaces.IProjectTaskService, DNTU.SkillBridge.Application.Workspaces.ProjectTaskService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Workspaces.ITaskCollaborationService, DNTU.SkillBridge.Application.Workspaces.TaskCollaborationService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Files.FileService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Files.IFileExpirationService>(provider => provider.GetRequiredService<DNTU.SkillBridge.Api.Files.FileService>());
builder.Services.AddScoped<DNTU.SkillBridge.Api.Milestones.MilestoneService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Meetings.MeetingService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Submissions.SubmissionService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Submissions.TechnicalReviewService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Submissions.BusinessReviewService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Academics.AcademicService>();
builder.Services.AddScoped<DNTU.SkillBridge.Api.Academics.AcademicEvaluationService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Payments.IPaymentService, DNTU.SkillBridge.Application.Payments.PaymentService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.SePay.ISePayService, DNTU.SkillBridge.Application.SePay.SePayService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Administration.IAdminGovernanceService, DNTU.SkillBridge.Application.Administration.AdminGovernanceService>();
builder.Services.AddScoped<DNTU.SkillBridge.Application.Analytics.IAnalyticsService, DNTU.SkillBridge.Application.Analytics.AnalyticsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<NotificationWriter>();
builder.Services.AddScoped<INotificationWriter>(provider => provider.GetRequiredService<NotificationWriter>());
builder.Services.AddScoped<IOutboxEnqueuer>(provider => provider.GetRequiredService<NotificationWriter>());
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<DNTU.SkillBridge.Infrastructure.Files.FileCleanupService>();
builder.Services.AddSignalR();
builder.Services.AddHttpClient(nameof(DNTU.SkillBridge.Api.Files.S3CompatibleFileStorage));
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
if (storageOptions.Enabled && (string.Equals(storageOptions.Provider, "MinIO", StringComparison.OrdinalIgnoreCase) || string.Equals(storageOptions.Provider, "S3", StringComparison.OrdinalIgnoreCase)))
{
    builder.Services.AddSingleton<DNTU.SkillBridge.Application.Files.IFileStorage, DNTU.SkillBridge.Api.Files.S3CompatibleFileStorage>();
}
else
{
    builder.Services.AddSingleton<DNTU.SkillBridge.Application.Files.IFileStorage, DNTU.SkillBridge.Application.Files.DisabledFileStorage>();
}
builder.Services.AddSingleton<IAccountEmailSender, NullAccountEmailSender>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<DNTU.SkillBridge.Domain.Identity.User>, Microsoft.AspNetCore.Identity.PasswordHasher<DNTU.SkillBridge.Domain.Identity.User>>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs")) context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.9"
        }, cancellationToken);
    };
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("password-recovery", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1.0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DNTU SkillBridge API",
        Version = "v1",
        Description = "Version 1 of the DNTU SkillBridge HTTP API."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await IdentitySeed.SeedAsync(dbContext, CancellationToken.None);
    await CatalogSeed.SeedAsync(dbContext, CancellationToken.None);
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    app.Logger.LogInformation(
        "HTTP {RequestMethod} {RequestPath} completed with {StatusCode} in {DurationMs}ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.Elapsed.TotalMilliseconds);
});
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ProjectHub>("/hubs/projects");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DNTU SkillBridge API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "DNTU SkillBridge API";
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");
app.MapControllers();

app.Run();

public partial class Program;
