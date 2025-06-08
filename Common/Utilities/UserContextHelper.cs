using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utilities
{
    public class UserContextHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContextHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var idClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(idClaim) ? Guid.Empty : Guid.Parse(idClaim);
        }

        public string GetCurrentUserRole()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        public bool IsAdmin()
        {
            return GetCurrentUserRole().Equals("admin", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsOwner(Guid providerId)
        {
            return !IsAdmin() && GetCurrentUserId() == providerId;
        }

        public bool IsSameUser(Guid userId)
        {
            return GetCurrentUserId() == userId;
        }
    }
}
