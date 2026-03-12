using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GateAccessLogController : BaseController
    {
        private static readonly List<string> DefaultUserTypes =
        [
            "Homeowner",
            "Visitor",
            "Staff",
            "Delivery"
        ];

        private static readonly List<string> DefaultAccessTypes =
        [
            "Entry",
            "Exit"
        ];

        public GateAccessLogController(IDataService data) : base(data)
        {
        }

        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null, string? search = null, string? userType = null, string? accessType = null)
        {
            var model = await BuildManagementViewModelAsync(startDate, endDate, search, userType, accessType);
            return PartialView("Index", model);
        }

        private async Task<AdminGateAccessLogViewModel> BuildManagementViewModelAsync(DateTime? startDate, DateTime? endDate, string? search, string? userType, string? accessType)
        {
            var nowUtc = DateTime.UtcNow;
            var rangeStart = startDate?.Date ?? nowUtc.Date.AddDays(-7);
            var rangeEnd = (endDate?.Date ?? nowUtc.Date).AddDays(1).AddTicks(-1);
            var normalizedSearch = search?.Trim();

            var logs = (await _data.GetGateAccessLogsAsync())
                .Where(log => log.AccessTime >= rangeStart && log.AccessTime <= rangeEnd)
                .OrderByDescending(log => log.AccessTime)
                .ToList();

            var homeownerIds = logs
                .Where(log => log.HomeownerID.HasValue)
                .Select(log => log.HomeownerID!.Value)
                .Distinct()
                .ToHashSet();

            var homeownerNames = (await _data.GetHomeownersAsync())
                .Where(homeowner => homeownerIds.Contains(homeowner.HomeownerID))
                .ToDictionary(homeowner => homeowner.HomeownerID, homeowner => homeowner.FullName ?? $"Homeowner #{homeowner.HomeownerID}");

            var mappedLogs = logs
                .Select(log => new GateAccessLogListItemViewModel
                {
                    LogID = log.LogID,
                    HomeownerID = log.HomeownerID,
                    DisplayName = ResolveDisplayName(log, homeownerNames),
                    UserType = log.UserType,
                    AccessType = log.AccessType,
                    AccessTime = log.AccessTime,
                    PlateNumber = log.PlateNumber,
                    GateLocation = log.GateLocation,
                    VerifiedBy = log.VerifiedBy,
                    Notes = log.Notes,
                    VisitorName = log.VisitorName
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                mappedLogs = mappedLogs
                    .Where(log => MatchesSearch(log, normalizedSearch))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(userType))
            {
                mappedLogs = mappedLogs
                    .Where(log => string.Equals(log.UserType, userType, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(accessType))
            {
                mappedLogs = mappedLogs
                    .Where(log => string.Equals(log.AccessType, accessType, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new AdminGateAccessLogViewModel
            {
                Logs = mappedLogs,
                Statistics = BuildStatistics(mappedLogs, rangeStart, rangeEnd),
                StartDate = rangeStart,
                EndDate = rangeEnd,
                SearchQuery = normalizedSearch ?? string.Empty,
                SelectedUserType = userType ?? string.Empty,
                SelectedAccessType = accessType ?? string.Empty,
                UserTypes = DefaultUserTypes,
                AccessTypes = DefaultAccessTypes
            };
        }

        private static GateAccessStatistics BuildStatistics(List<GateAccessLogListItemViewModel> logs, DateTime startDate, DateTime endDate)
        {
            return new GateAccessStatistics
            {
                TotalEntries = logs.Count(log => string.Equals(log.AccessType, "Entry", StringComparison.OrdinalIgnoreCase)),
                TotalExits = logs.Count(log => string.Equals(log.AccessType, "Exit", StringComparison.OrdinalIgnoreCase)),
                HomeownerEntries = logs.Count(log => string.Equals(log.UserType, "Homeowner", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(log.AccessType, "Entry", StringComparison.OrdinalIgnoreCase)),
                VisitorEntries = logs.Count(log => string.Equals(log.UserType, "Visitor", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(log.AccessType, "Entry", StringComparison.OrdinalIgnoreCase)),
                StaffEntries = logs.Count(log => string.Equals(log.UserType, "Staff", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(log.AccessType, "Entry", StringComparison.OrdinalIgnoreCase)),
                DeliveryEntries = logs.Count(log => string.Equals(log.UserType, "Delivery", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(log.AccessType, "Entry", StringComparison.OrdinalIgnoreCase)),
                StartDate = startDate,
                EndDate = endDate
            };
        }

        private static string ResolveDisplayName(GateAccessLog log, IReadOnlyDictionary<int, string> homeownerNames)
        {
            if (!string.IsNullOrWhiteSpace(log.VisitorName))
            {
                return log.VisitorName;
            }

            if (log.HomeownerID.HasValue && homeownerNames.TryGetValue(log.HomeownerID.Value, out var homeownerName))
            {
                return homeownerName;
            }

            if (log.HomeownerID.HasValue && string.Equals(log.UserType, "Homeowner", StringComparison.OrdinalIgnoreCase))
            {
                return $"Homeowner #{log.HomeownerID.Value}";
            }

            return log.UserType;
        }

        private static bool MatchesSearch(GateAccessLogListItemViewModel log, string query)
        {
            var comparison = StringComparison.OrdinalIgnoreCase;
            return (!string.IsNullOrWhiteSpace(log.DisplayName) && log.DisplayName.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.UserType) && log.UserType.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.AccessType) && log.AccessType.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.PlateNumber) && log.PlateNumber.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.GateLocation) && log.GateLocation.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.VerifiedBy) && log.VerifiedBy.Contains(query, comparison))
                || (!string.IsNullOrWhiteSpace(log.Notes) && log.Notes.Contains(query, comparison));
        }
    }
}
