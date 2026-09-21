using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace StudentCourseManagement.Application.Interfaces;

public interface IAdminNotificationService
{
    Task NotifyPendingRequestCreatedAsync(int studentId, int courseId, string requestType, string? reason);
    Task NotifyPendingRequestProcessedAsync(int requestId, bool approve);
}