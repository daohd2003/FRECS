using BusinessObject.Enums;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Hubs
{
    /// <summary>
    /// Hub để track active users trong real-time cho admin dashboard
    /// </summary>
    // [Authorize] - Commented out vì có conflict với SignalR authentication flow
    // Authentication được handle bằng cách check Context.User trong OnConnectedAsync
    public class ActiveUsersHub : Hub
    {
        // Thread-safe dictionary để track user connections
        // Key: UserId, Value: UserConnectionInfo
        private static readonly ConcurrentDictionary<string, UserConnectionInfo> ActiveUsers 
            = new ConcurrentDictionary<string, UserConnectionInfo>();

        // Dictionary để track pending disconnections (cho phép reconnect trong 10s)
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> PendingDisconnects 
            = new ConcurrentDictionary<string, CancellationTokenSource>();

        // Static IHubContext để có thể broadcast từ background tasks
        private static IHubContext<ActiveUsersHub>? _hubContext;

        private readonly ShareItDbContext _context;

        public ActiveUsersHub(ShareItDbContext context)
        {
            _context = context;
        }

        // Static method để set HubContext từ bên ngoài (được gọi từ Program.cs)
        public static void SetHubContext(IHubContext<ActiveUsersHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                // Manual authentication check
                if (Context.User == null || Context.User.Identity?.IsAuthenticated != true)
                {
                    Context.Abort();
                    return;
                }

                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value ?? 
                               Context.User?.FindFirst("email")?.Value;
                var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                // Cancel pending disconnect nếu user reconnect
                if (PendingDisconnects.TryRemove(userId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                var isNewConnection = !ActiveUsers.ContainsKey(userId);
                UserConnectionInfo connectionInfo;

                // OPTIMIZATION: Chỉ query database nếu user CHƯA CÓ trong cache
                // Giảm database queries khi user navigate giữa các pages
                if (isNewConnection)
                {
                    // Query database chỉ cho new connection
                    var user = await _context.Users
                        .Include(u => u.Profile)
                        .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

                    connectionInfo = new UserConnectionInfo
                    {
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        Email = userEmail ?? user?.Email ?? "Unknown",
                        FullName = user?.Profile?.FullName ?? "Unknown User",
                        Role = userRole ?? user?.Role.ToString() ?? "Unknown",
                        AvatarUrl = user?.Profile?.ProfilePictureUrl,
                        ConnectedAt = DateTime.UtcNow,
                        LastActivity = DateTime.UtcNow
                    };
                }
                else
                {
                    // Reuse cached info for reconnection (page navigation)
                    var existingInfo = ActiveUsers[userId];
                    connectionInfo = new UserConnectionInfo
                    {
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        Email = existingInfo.Email,
                        FullName = existingInfo.FullName,
                        Role = existingInfo.Role,
                        AvatarUrl = existingInfo.AvatarUrl,
                        ConnectedAt = existingInfo.ConnectedAt, // Keep original connect time
                        LastActivity = DateTime.UtcNow
                    };
                }

                ActiveUsers.AddOrUpdate(userId, connectionInfo, (key, existing) =>
                {
                    // Nếu user đã có connection, update thông tin
                    existing.ConnectionId = Context.ConnectionId;
                    existing.LastActivity = DateTime.UtcNow;
                    return existing;
                });

                // Join admin group để nhận updates
                if (userRole == "admin" || userRole == "staff")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "AdminGroup");
                }
                
                // Chỉ broadcast nếu là connection MỚI (không phải reconnect)
                if (isNewConnection)
                {
                    await Clients.Group("AdminGroup").SendAsync("UserConnected", connectionInfo);
                }
                
                // Send current active users to the newly connected user (if admin)
                if (userRole == "admin" || userRole == "staff")
                {
                    var activeUsersList = GetActiveUsersList();
                    await Clients.Caller.SendAsync("ActiveUsersUpdate", activeUsersList);
                }
            }

            await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                // Don't abort connection on error, just log it
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // DELAY 10 giây trước khi xóa user (cho phép page reload/navigation)
                var cts = new CancellationTokenSource();
                PendingDisconnects.AddOrUpdate(userId, cts, (key, existing) =>
                {
                    existing.Cancel();
                    existing.Dispose();
                    return cts;
                });

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(10000, cts.Token); // Đợi 10 giây
                        
                        // Nếu không bị cancel (user không reconnect), xóa user
                        if (!cts.Token.IsCancellationRequested)
                        {
                            PendingDisconnects.TryRemove(userId, out _);
                            
                            if (ActiveUsers.TryRemove(userId, out var removedUser))
                            {
                                // Broadcast to all admins về user offline (dùng _hubContext vì đã mất Hub context)
                                if (_hubContext != null)
                                {
                                    await _hubContext.Clients.Group("AdminGroup").SendAsync("UserDisconnected", userId);
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // User reconnected, không làm gì
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                });
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Client gọi để update last activity time (optional, dùng cho "away" status)
        /// </summary>
        public async Task UpdateActivity()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId) && ActiveUsers.TryGetValue(userId, out var user))
            {
                user.LastActivity = DateTime.UtcNow;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Lấy danh sách tất cả active users (for admin)
        /// </summary>
        public async Task<List<ActiveUserDto>> GetActiveUsers()
        {
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            // Chỉ admin và staff mới có thể xem
            if (userRole != "admin" && userRole != "staff")
            {
                return new List<ActiveUserDto>();
            }

            return GetActiveUsersList();
        }

        /// <summary>
        /// Helper method để lấy danh sách active users
        /// </summary>
        private List<ActiveUserDto> GetActiveUsersList()
        {
            var now = DateTime.UtcNow;
            
            return ActiveUsers.Values
                .Select(u => new ActiveUserDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    AvatarUrl = u.AvatarUrl,
                    ConnectedAt = u.ConnectedAt,
                    LastActivity = u.LastActivity,
                    Status = GetUserStatus(u.LastActivity, now),
                    OnlineDuration = GetOnlineDuration(u.ConnectedAt, now)
                })
                .OrderByDescending(u => u.LastActivity)
                .ToList();
        }

        private string GetUserStatus(DateTime lastActivity, DateTime now)
        {
            var idleMinutes = (now - lastActivity).TotalMinutes;
            
            if (idleMinutes < 5) return "online";
            if (idleMinutes < 15) return "away";
            return "idle";
        }

        private string GetOnlineDuration(DateTime connectedAt, DateTime now)
        {
            var duration = now - connectedAt;
            
            if (duration.TotalMinutes < 1) return "Just now";
            if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes}m ago";
            if (duration.TotalHours < 24) return $"{(int)duration.TotalHours}h ago";
            return $"{(int)duration.TotalDays}d ago";
        }
    }

    // Models cho Active Users tracking
    public class UserConnectionInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class ActiveUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public string Status { get; set; } = "online"; // online, away, idle
        public string OnlineDuration { get; set; } = string.Empty;
    }
}

