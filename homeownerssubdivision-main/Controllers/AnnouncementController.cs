using HOMEOWNER.Data;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize(Roles = "Homeowner")]
    public class AnnouncementController : BaseController
    {
        public AnnouncementController(IDataService data) : base(data)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var announcements = (await _data.GetAnnouncementsAsync())
                .OrderByDescending(a => a.PostedAt)
                .ToList();

            var model = new HomeownerAnnouncementsViewModel
            {
                Announcements = announcements
            };

            return PartialView("Index", model);
        }
    }
}
