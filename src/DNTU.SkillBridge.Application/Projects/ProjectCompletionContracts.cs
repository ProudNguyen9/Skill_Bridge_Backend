namespace DNTU.SkillBridge.Application.Projects;

public sealed record ProjectCompletionResponse(Guid Id, Guid ProjectId, Guid CompletedByUserId, DateTimeOffset CompletedAt, string EvidenceJson);
