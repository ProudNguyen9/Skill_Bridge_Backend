using System.Security.Cryptography;
using System.Text;
using DNTU.SkillBridge.Application.Abstractions;
using DNTU.SkillBridge.Application.Common.Options;
using DNTU.SkillBridge.Application.Notifications;
using DNTU.SkillBridge.Domain.Payments;
using Microsoft.Extensions.Options;

namespace DNTU.SkillBridge.Application.SePay;

public interface ISePayService
{
    Task<SePayCheckoutResponse?> CreateCheckoutAsync(Guid companyUserId, Guid fundingOrderId, CancellationToken cancellationToken);

    Task<bool> ApplyIpnAsync(SePayIpnRequest request, CancellationToken cancellationToken);
}
