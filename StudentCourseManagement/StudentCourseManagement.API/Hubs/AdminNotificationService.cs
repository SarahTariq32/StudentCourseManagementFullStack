using Microsoft.AspNetCore.SignalR;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Hubs;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IHubContext<AdminHub> _hubContext;

    public AdminNotificationService(IHubContext<AdminHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyPendingRequestCreatedAsync(int studentId, int courseId, string requestType, string? reason)
    {
        await _hubContext.Clients.Group(AdminHub.AdminsGroup)
            .SendAsync("PendingRequestCreated", new { studentId, courseId, requestType, reason });
    }

}