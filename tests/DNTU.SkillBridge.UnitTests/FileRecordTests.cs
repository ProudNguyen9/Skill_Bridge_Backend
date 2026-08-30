using DNTU.SkillBridge.Domain.Files;

namespace DNTU.SkillBridge.UnitTests;

public sealed class FileRecordTests
{
    [Fact]
    public void Complete_StoresChecksumAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var file = new FileRecord(Guid.NewGuid(), null, "resume.pdf", "uploads/random.pdf", "application/pdf", 10, now.AddMinutes(1));

        file.Complete(new string('a', 64), now);

        Assert.Equal(FileUploadStatus.COMPLETED, file.Status);
        Assert.Equal(new string('a', 64), file.ChecksumSha256);
        Assert.Equal(now, file.CompletedAt);
    }

    [Fact]
    public void Expire_OnlyExpiresPendingUpload()
    {
        var now = DateTimeOffset.UtcNow;
        var file = new FileRecord(Guid.NewGuid(), null, "resume.pdf", "uploads/random.pdf", "application/pdf", 10, now.AddMinutes(-1));

        file.Expire(now);

        Assert.Equal(FileUploadStatus.EXPIRED, file.Status);
    }

    [Fact]
    public void Complete_RejectsExpiredUploadRequest()
    {
        var file = new FileRecord(Guid.NewGuid(), null, "resume.pdf", "uploads/random.pdf", "application/pdf", 10, DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(() => file.Complete(new string('a', 64), DateTimeOffset.UtcNow));
    }
}
