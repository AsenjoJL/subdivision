using HOMEOWNER.Models;

namespace HOMEOWNER.Models.ViewModels
{
    public class HomeownerEventsViewModel
    {
        public List<EventModel> Events { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public int TotalEvents => Events.Count;
        public int UpcomingEvents => Events.Count(e => e.EventDate >= DateTime.Today);
        public int ThisMonthEvents => Events.Count(e => e.EventDate.Year == DateTime.Today.Year && e.EventDate.Month == DateTime.Today.Month);
        public EventModel? NextEvent => Events
            .Where(e => e.EventDate >= DateTime.Now)
            .OrderBy(e => e.EventDate)
            .FirstOrDefault();
    }
}
