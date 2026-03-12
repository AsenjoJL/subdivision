namespace HOMEOWNER.Models.ViewModels
{
    public class AdminAnalyticsViewModel
    {
        public List<AdminAnalyticsMetricViewModel> Metrics { get; set; } = new();
        public List<AdminAnalyticsTrendPointViewModel> MonthlyTrend { get; set; } = new();
        public List<AdminAnalyticsFinancialTrendPointViewModel> FinancialTrend { get; set; } = new();
        public List<AdminAnalyticsBreakdownItemViewModel> ReservationBreakdown { get; set; } = new();
        public List<AdminAnalyticsBreakdownItemViewModel> BillingBreakdown { get; set; } = new();
        public List<AdminAnalyticsBreakdownItemViewModel> ServiceRequestBreakdown { get; set; } = new();
        public List<AdminAnalyticsCategoryPointViewModel> FacilityPerformance { get; set; } = new();
        public List<AdminAnalyticsCategoryPointViewModel> ServiceCategoryPerformance { get; set; } = new();
        public List<AdminAnalyticsCategoryPointViewModel> CommunitySignals { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class AdminAnalyticsMetricViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Tone { get; set; } = "blue";
    }

    public class AdminAnalyticsTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Reservations { get; set; }
        public int ServiceRequests { get; set; }
        public int Billings { get; set; }
    }

    public class AdminAnalyticsFinancialTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal CollectedRevenue { get; set; }
        public decimal OutstandingRevenue { get; set; }
    }

    public class AdminAnalyticsBreakdownItemViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Tone { get; set; } = "blue";
    }

    public class AdminAnalyticsCategoryPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Subtitle { get; set; } = string.Empty;
    }
}
