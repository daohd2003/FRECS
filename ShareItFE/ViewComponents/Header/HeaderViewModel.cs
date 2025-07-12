using BusinessObject.DTOs.NotificationDto;

namespace ShareItFE.ViewComponents.Header
{
    public class HeaderViewModel
    {
        public bool IsUserLoggedIn { get; set; }
        public string? UserName { get; set; }
        public string? UserAvatarUrl { get; set; }
        public string? UserRole { get; set; }
        public int CartItemCount { get; set; }

        public int UnreadNotificationCount { get; set; }
        public List<NotificationResponse> Notifications { get; set; } = new List<NotificationResponse>();
        public Guid UserId { get; set; }
        public string? AccessToken { get; set; }
    }
}
