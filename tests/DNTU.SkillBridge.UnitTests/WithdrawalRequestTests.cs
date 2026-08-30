using DNTU.SkillBridge.Domain.Commitments;

namespace DNTU.SkillBridge.UnitTests;

public sealed class WithdrawalRequestTests
{
    [Fact]
    public void Expired_pending_commitment_is_recorded_as_abandoned()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(-1);
        var commitment = new ProjectCommitment(Guid.NewGuid(), Guid.NewGuid(), deadline);

        commitment.Expire(DateTimeOffset.UtcNow);

        Assert.Equal(CommitmentStatus.ABANDONED, commitment.Status);
        Assert.Throws<InvalidOperationException>(() => commitment.Confirm(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Recommend_then_approve_records_an_immutable_decision_history()
    {
        var withdrawal = new WithdrawalRequest(Guid.NewGuid(), Guid.NewGuid(), "Không thể tiếp tục vì lý do sức khỏe.");
        var lecturerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        withdrawal.Recommend(lecturerId, now, "Đề nghị phê duyệt.");
        withdrawal.Decide(adminId, now.AddMinutes(1), approve: true, "Đã phê duyệt.");

        Assert.Equal(WithdrawalStatus.APPROVED, withdrawal.Status);
        Assert.Equal(lecturerId, withdrawal.RecommendedByLecturerId);
        Assert.Equal(adminId, withdrawal.DecidedByUserId);
        Assert.Throws<InvalidOperationException>(() => withdrawal.Decide(adminId, now, approve: false, null));
    }

    [Fact]
    public void Decision_without_a_valid_transition_is_rejected()
    {
        var withdrawal = new WithdrawalRequest(Guid.NewGuid(), Guid.NewGuid(), "Lịch học thay đổi.");

        withdrawal.Decide(Guid.NewGuid(), DateTimeOffset.UtcNow, approve: false, null);

        Assert.Equal(WithdrawalStatus.REJECTED, withdrawal.Status);
        Assert.Throws<InvalidOperationException>(() => withdrawal.Recommend(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
    }
}
