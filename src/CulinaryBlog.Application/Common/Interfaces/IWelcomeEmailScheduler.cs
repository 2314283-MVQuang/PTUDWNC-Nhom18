namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>FR-JOB-001: lên lịch sau khi tài khoản và refresh token đã được lưu.</summary>
public interface IWelcomeEmailScheduler
{
    string Enqueue(string userId);
}
