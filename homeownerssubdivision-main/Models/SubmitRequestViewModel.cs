namespace HOMEOWNER.Models.ViewModels
{
    public class SubmitRequestViewModel
    {
        public ServiceRequest? NewRequest { get; set; }
        public List<ServiceRequest>? ServiceRequests { get; set; }
        public int HomeownerId { get; set; }
        public string? HomeownerName { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
