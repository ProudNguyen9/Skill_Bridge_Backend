using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DNTU.SkillBridge.Infrastructure.Realtime;

[Authorize]
public sealed class NotificationHub : Hub { }
