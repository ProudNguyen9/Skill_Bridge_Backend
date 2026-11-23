using DNTU.SkillBridge.Application.Social;
using DNTU.SkillBridge.Domain.Social;
using DNTU.SkillBridge.Infrastructure.Persistence;

namespace DNTU.SkillBridge.Infrastructure.Repositories;

/// <summary>Ghi SocialActivity cùng DbContext với caller (transactional mirror của INotificationWriter).</summary>
public sealed class SocialActivityWriter(AppDbContext dbContext) : ISocialActivityWriter
{
    public void Add(SocialActivityMessage message)
    {
        dbContext.SocialActivities.Add(new SocialActivity(
            message.ActorUserId,
            message.ActivityType,
            message.Title,
            message.Summary,
            message.LinkUrl,
            message.TargetEntityId));
    }
}
