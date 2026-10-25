namespace DNTU.SkillBridge.Api.Authentication;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    public const string DntuHostedDomain = "dntu.edu.vn";

    public string ClientId { get; init; } = string.Empty;
}
