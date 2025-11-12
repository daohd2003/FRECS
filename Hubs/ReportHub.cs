using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Hubs
{
    [Authorize(Roles = "admin,staff")]
    public class ReportHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> UserConnections = new();
        private readonly ShareItDbContext _context;

        public ReportHub(ShareItDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                var connectionId = Context.ConnectionId;
                UserConnections[userId] = connectionId;

                await Groups.AddToGroupAsync(connectionId, "Admins");

                var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                Console.WriteLine($"--> {userRole} connected: {Context.User.Identity?.Name} with ConnectionId: {connectionId}");

                // Gửi danh sách report chưa được gán cho bất kỳ ai (Pending)
                var reports = await _context.Reports
                    .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                    .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        subject = r.Subject,
                        reporterName = r.Reporter.Profile.FullName,
                        reporteeName = r.Reportee.Profile.FullName,
                        dateCreated = r.CreatedAt,
                        status = r.Status.ToString(),
                        isTaskAssigned = r.AssignedAdminId != null
                    })
                    .ToListAsync();


                await Clients.Caller.SendAsync("ReceiveReportList", reports);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                UserConnections.TryRemove(userId, out _);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
                var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                Console.WriteLine($"--> {userRole} disconnected: {Context.User.Identity?.Name}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public static string? GetConnectionIdForUser(string userId)
        {
            UserConnections.TryGetValue(userId, out var connectionId);
            return connectionId;
        }

        /// <summary>
        /// Gửi danh sách báo cáo mới đến tất cả admin đang online
        /// </summary>
        public async Task SendReportListAsync()
        {
            var reports = await _context.Reports
                .Include(r => r.Reporter).ThenInclude(u => u.Profile)
                .Include(r => r.Reportee).ThenInclude(u => u.Profile)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    subject = r.Subject,
                    reporterName = r.Reporter.Profile.FullName,
                    reporteeName = r.Reportee.Profile.FullName,
                    dateCreated = r.CreatedAt,
                    status = r.Status.ToString(),
                    isTaskAssigned = r.AssignedAdminId != null
                })
                .ToListAsync();


            await Clients.Group("Admins").SendAsync("ReceiveReportList", reports);
        }
    }
}
