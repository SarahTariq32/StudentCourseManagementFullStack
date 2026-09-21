using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StudentCourseManagement.API.Hubs;

[Authorize]
public class AdminHub : Hub
{
    public const string AdminsGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true || Context.User?.IsInRole("admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.IsInRole("Admin") == true || Context.User?.IsInRole("admin") == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        await base.OnDisconnectedAsync(exception);
    }
}