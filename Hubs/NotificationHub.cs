using Microsoft.AspNetCore.SignalR;

namespace Hubs
{
    public class NotificationHub : Hub
    {
        public async Task JoinCustomerNotificationGroup(Guid userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"notifications-{userId}");
        }
    }
}
