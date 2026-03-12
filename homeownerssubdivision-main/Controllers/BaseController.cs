using HOMEOWNER.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly IDataService _data;

        protected BaseController(IDataService data)
        {
            _data = data;
        }

        protected string? GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value;
        }

        protected string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }

        protected string? GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value;
        }

        protected int GetCurrentHomeownerId()
        {
            var sessionId = HttpContext.Session.GetInt32("HomeownerID");
            if (sessionId.HasValue)
            {
                return sessionId.Value;
            }

            return GetClaimIntValue("HomeownerID");
        }

        protected int GetCurrentAdminId()
        {
            var sessionId = HttpContext.Session.GetInt32("AdminID");
            if (sessionId.HasValue)
            {
                return sessionId.Value;
            }

            return GetClaimIntValue("AdminID");
        }

        protected int GetCurrentStaffId()
        {
            var sessionId = HttpContext.Session.GetInt32("StaffID");
            if (sessionId.HasValue)
            {
                return sessionId.Value;
            }

            return GetClaimIntValue("StaffID");
        }

        protected string? GetCurrentStaffPosition()
        {
            var sessionPosition = HttpContext.Session.GetString("StaffRole");
            if (!string.IsNullOrWhiteSpace(sessionPosition))
            {
                return sessionPosition;
            }

            return User.FindFirst("Position")?.Value;
        }

        protected bool IsAdmin()
        {
            return GetCurrentUserRole() == "Admin";
        }

        protected bool IsHomeowner()
        {
            return GetCurrentUserRole() == "Homeowner";
        }

        protected bool IsStaff()
        {
            return GetCurrentUserRole() == "Staff";
        }

        private int GetClaimIntValue(string claimType)
        {
            var claimValue = User.FindFirst(claimType)?.Value;
            return int.TryParse(claimValue, out var id) ? id : 0;
        }
    }
}
