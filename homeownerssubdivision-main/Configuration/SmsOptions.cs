namespace HOMEOWNER.Configuration
{
    public class SmsOptions
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = "https://www.iprogsms.com/api/v1/sms_messages";
        public string ApiToken { get; set; } = string.Empty;
        public int SmsProvider { get; set; }
        public string DefaultCountryCode { get; set; } = "+63";
    }
}
