using System.Collections.Concurrent;
using DNTU.SkillBridge.Application.Chat;

namespace DNTU.SkillBridge.Api.Realtime;

/// <summary>PresenceTracker in-memory (single-node). Sẵn sàng thay Redis backplane khi scale-out.</summary>
public sealed class ChatPresenceTracker : IChatPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> connections = new();

    public bool Connect(Guid userId, string connectionId)
    {
        var userConnections = connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        userConnections.TryAdd(connectionId, 0);
        return userConnections.Count == 1;
    }

    public bool Disconnect(Guid userId, string connectionId)
    {
        if (!connections.TryGetValue(userId, out var userConnections)) return false;
        userConnections.TryRemove(connectionId, out _);
        if (userConnections.IsEmpty)
        {
            connections.TryRemove(userId, out _);
            return true;
        }

        return false;
    }

    public bool IsOnline(Guid userId) => connections.ContainsKey(userId) && !connections[userId].IsEmpty;

    public IReadOnlyCollection<Guid> GetOnlineUserIds(IReadOnlyCollection<Guid> userIds) =>
        userIds.Where(IsOnline).ToList();

    public IReadOnlyCollection<Guid> GetAllOnlineUserIds() => connections.Keys.ToList();
}
