using BusinessObject.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.NotificationDto
{
    public class ManualNotificationRequest
    {
        public Guid UserId { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }
    }
}
