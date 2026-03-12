using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner,Admin")]
    public class ReservationController : BaseController
    {
        public ReservationController(IDataService data) : base(data)
        {
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetReservationEndUtc(Reservation reservation)
        {
            return DateTime.SpecifyKind(reservation.ReservationDate.Date + reservation.EndTime, DateTimeKind.Utc);
        }

        private async Task<List<Reservation>> GetBlockingReservationsAsync()
        {
            var approvedReservationsTask = _data.GetReservationsByStatusAsync("Approved");
            var pendingReservationsTask = _data.GetReservationsByStatusAsync("Pending");

            await Task.WhenAll(approvedReservationsTask, pendingReservationsTask);

            return approvedReservationsTask.Result
                .Concat(pendingReservationsTask.Result)
                .ToList();
        }

        private static List<ReservedFacilitySlotViewModel> BuildReservedSlotViewModels(IEnumerable<Reservation> reservations)
        {
            return reservations
                .Select(r => new ReservedFacilitySlotViewModel
                {
                    FacilityId = r.FacilityID,
                    Date = r.ReservationDate.Date,
                    Start = r.StartTime.ToString(@"hh\:mm"),
                    End = r.EndTime.ToString(@"hh\:mm")
                })
                .OrderByDescending(slot => slot.Date)
                .ThenBy(slot => slot.Start)
                .ToList();
        }

        private async Task<ReservationIndexViewModel> BuildIndexViewModelAsync(int homeownerId)
        {
            var facilitiesTask = _data.GetAvailableFacilitiesAsync();
            var activityCountTask = _data.GetReservationCountByHomeownerIdAndStatusAsync(homeownerId, "Approved");
            var blockingReservationsTask = GetBlockingReservationsAsync();

            await Task.WhenAll(facilitiesTask, activityCountTask, blockingReservationsTask);

            var reservedSlots = BuildReservedSlotViewModels(blockingReservationsTask.Result);

            return new ReservationIndexViewModel
            {
                Facilities = facilitiesTask.Result,
                ReservedSlots = reservedSlots,
                ActivityCount = activityCountTask.Result,
                IsEmbedded = IsAjaxRequest(),
                SuccessMessage = TempData["Success"] as string,
                ErrorMessage = TempData["Error"] as string
            };
        }

        [HttpGet]
        public async Task<IActionResult> ReservedSlots(int facilityId)
        {
            if (facilityId <= 0)
            {
                return Json(new { success = false, message = "Invalid facility." });
            }

            var blockingReservations = await GetBlockingReservationsAsync();
            var slots = BuildReservedSlotViewModels(
                blockingReservations.Where(r => r.FacilityID == facilityId));

            return Json(new
            {
                success = true,
                slots = slots.Select(slot => new
                {
                    facilityId = slot.FacilityId,
                    date = slot.Date.ToString("yyyy-MM-dd"),
                    start = slot.Start,
                    end = slot.End
                })
            });
        }

        private async Task ExpireReservationsAsync(IEnumerable<Reservation> reservations, DateTime nowUtc)
        {
            var expiredReservations = reservations
                .Where(r => string.Equals(r.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                .Where(r => GetReservationEndUtc(r) <= nowUtc)
                .ToList();

            foreach (var reservation in expiredReservations)
            {
                reservation.Status = "Expired";
                reservation.UpdatedAt = nowUtc;
                await _data.UpdateReservationAsync(reservation);
            }
        }

        public async Task<IActionResult> Index()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildIndexViewModelAsync(homeownerId);
            return IsAjaxRequest() ? PartialView("Index", model) : View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ReserveFacility([FromForm] ReservationRequestViewModel request)
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                var message = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Please complete all reservation fields.";

                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message });
                }

                TempData["Error"] = message;
                return RedirectToAction("Index");
            }

            var requestedDate = DateTime.SpecifyKind(request.ReservationDate.Date, DateTimeKind.Utc);
            if (requestedDate < DateTime.UtcNow.Date)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Reservation date cannot be in the past." });
                }

                TempData["Error"] = "Reservation date cannot be in the past.";
                return RedirectToAction("Index");
            }

            if (request.StartTime >= request.EndTime)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "End time must be later than start time." });
                }

                TempData["Error"] = "End time must be later than start time.";
                return RedirectToAction("Index");
            }

            var homeownerTask = _data.GetHomeownerByIdAsync(homeownerId);
            var facilityTask = _data.GetFacilityByIdAsync(request.FacilityId);
            var approvedReservationsTask = _data.GetReservationsByStatusAsync("Approved");
            var pendingReservationsTask = _data.GetReservationsByStatusAsync("Pending");

            await Task.WhenAll(homeownerTask, facilityTask, approvedReservationsTask, pendingReservationsTask);

            var homeowner = homeownerTask.Result;
            var facility = facilityTask.Result;

            if (homeowner == null || facility == null)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Invalid homeowner or facility." });
                }

                TempData["Error"] = "Invalid homeowner or facility.";
                return RedirectToAction("Index");
            }

            var nowUtc = DateTime.UtcNow;
            var reservationDate = requestedDate;
            var approvedReservations = approvedReservationsTask.Result;
            await ExpireReservationsAsync(approvedReservations, nowUtc);

            var activeApprovedReservations = approvedReservations
                .Where(r => GetReservationEndUtc(r) > nowUtc)
                .ToList();

            var facilityReservations = activeApprovedReservations
                .Concat(pendingReservationsTask.Result)
                .ToList();

            var isConflict = facilityReservations.Any(r =>
                r.FacilityID == request.FacilityId &&
                r.ReservationDate.Date == reservationDate &&
                r.StartTime < request.EndTime &&
                r.EndTime > request.StartTime);

            if (isConflict)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "This facility is already reserved for the selected time slot." });
                }

                TempData["Error"] = "This facility is already reserved for the selected time slot.";
                return RedirectToAction("Index");
            }

            var reservation = new Reservation
            {
                HomeownerID = homeownerId,
                FacilityID = request.FacilityId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? "No Purpose Provided" : request.Purpose.Trim(),
                ReservationDate = reservationDate,
                Status = "Approved",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _data.AddReservationAsync(reservation);
                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        message = "Facility reserved successfully. Your booking is confirmed.",
                        reservation = new
                        {
                            facilityId = reservation.FacilityID,
                            date = reservation.ReservationDate.ToString("yyyy-MM-dd"),
                            start = reservation.StartTime.ToString(@"hh\:mm"),
                            end = reservation.EndTime.ToString(@"hh\:mm")
                        }
                    });
                }

                TempData["Success"] = "Facility reserved successfully. Your booking is confirmed.";
            }
            catch (Exception ex)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = $"Database error: {ex.Message}" });
                }

                TempData["Error"] = $"Database error: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> History()
        {
            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var reservations = await _data.GetReservationsByHomeownerIdAsync(homeownerId);
            await ExpireReservationsAsync(reservations, DateTime.UtcNow);

            var historyReservations = reservations
                .Where(r => r.Status == "Expired" || r.Status == "Canceled" || r.Status == "Cancelled")
                .OrderByDescending(r => r.ReservationDate)
                .ToList();

            var model = new ReservationHistoryViewModel
            {
                Reservations = historyReservations,
                IsEmbedded = IsAjaxRequest()
            };

            return IsAjaxRequest() ? PartialView("History", model) : View(model);
        }

        public async Task<IActionResult> ViewExpiredHistory()
        {
            await ExpireReservationsAsync(await _data.GetReservationsByStatusAsync("Approved"), DateTime.UtcNow);

            var expiredReservations = (await _data.GetReservationsAsync())
                .Where(r => string.Equals(r.Status, "Expired", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.ReservationDate)
                .ToList();

            return View(expiredReservations);
        }
    }
}
