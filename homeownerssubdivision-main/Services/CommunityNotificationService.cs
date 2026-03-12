using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;
using HOMEOWNER.Configuration;
using HOMEOWNER.Data;
using HOMEOWNER.Models;
using Microsoft.Extensions.Options;

namespace HOMEOWNER.Services
{
    public class CommunityNotificationService : ICommunityNotificationService
    {
        private readonly IDataService _data;
        private readonly EmailOptions _emailOptions;
        private readonly SmsOptions _smsOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CommunityNotificationService> _logger;

        public CommunityNotificationService(
            IDataService data,
            IOptions<EmailOptions> emailOptions,
            IOptions<SmsOptions> smsOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<CommunityNotificationService> logger)
        {
            _data = data;
            _emailOptions = emailOptions.Value;
            _smsOptions = smsOptions.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task NotifyAllHomeownersAnnouncementAsync(Announcement announcement, CancellationToken cancellationToken = default)
        {
            var homeowners = await _data.GetHomeownersAsync();
            var activeHomeowners = homeowners.Where(h => h.IsActive).ToList();
            var message = string.IsNullOrWhiteSpace(announcement.Content)
                ? "A new announcement was posted in the subdivision portal."
                : announcement.Content;

            foreach (var homeowner in activeHomeowners)
            {
                await DeliverAsync(
                    BuildNotification(
                        homeowner,
                        announcement.IsUrgent ? "Urgent community announcement" : "New community announcement",
                        message,
                        category: announcement.IsUrgent ? "UrgentAnnouncement" : "Announcement",
                        relatedEntityType: "Announcement",
                        relatedEntityId: announcement.AnnouncementID),
                    sendEmail: true,
                    sendSms: announcement.IsUrgent,
                    cancellationToken);
            }
        }

        public Task NotifyBillingCreatedAsync(Billing billing, Homeowner homeowner, CancellationToken cancellationToken = default)
        {
            var message = $"A new {billing.BillType} bill for Php {billing.Amount:N2} has been posted. Due date: {billing.DueDate:MMM dd, yyyy}.";
            return DeliverAsync(
                BuildNotification(homeowner, "New billing record", message, "Billing", "Billing", billing.BillingID),
                sendEmail: true,
                sendSms: false,
                cancellationToken);
        }

        public Task NotifyReservationStatusAsync(Reservation reservation, Homeowner homeowner, Facility? facility, bool approved, CancellationToken cancellationToken = default)
        {
            var facilityName = facility?.FacilityName ?? $"facility #{reservation.FacilityID}";
            var title = approved ? "Reservation approved" : "Reservation rejected";
            var message = approved
                ? $"Your reservation for {facilityName} on {reservation.ReservationDate:MMM dd, yyyy} has been approved."
                : $"Your reservation for {facilityName} on {reservation.ReservationDate:MMM dd, yyyy} has been rejected.";

            return DeliverAsync(
                BuildNotification(homeowner, title, message, "Reservation", "Reservation", reservation.ReservationID),
                sendEmail: true,
                sendSms: false,
                cancellationToken);
        }

        public async Task NotifyBillingSubmissionReceivedAsync(Billing billing, Homeowner homeowner, CancellationToken cancellationToken = default)
        {
            var admins = await _data.GetAdminsAsync();
            var message = $"{homeowner.FullName} submitted a payment for bill #{billing.BillingID} amounting to Php {(billing.SubmittedAmount ?? billing.Amount):N2}.";

            foreach (var admin in admins)
            {
                await DeliverAsync(
                    new Notification
                    {
                        Title = "Payment submission received",
                        Message = message,
                        Category = "BillingSubmission",
                        RecipientRole = "Admin",
                        RecipientUserId = admin.AdminID,
                        RecipientName = admin.FullName,
                        RecipientEmail = admin.Email,
                        CreatedAt = DateTime.UtcNow,
                        RelatedEntityType = "Billing",
                        RelatedEntityId = billing.BillingID
                    },
                    sendEmail: true,
                    sendSms: false,
                    cancellationToken);
            }
        }

        public async Task NotifyHomeownerAccountCreatedAsync(Homeowner homeowner, string temporaryPassword, string loginUrl, CancellationToken cancellationToken = default)
        {
            var normalizedLoginUrl = string.IsNullOrWhiteSpace(loginUrl) ? "/Account/Login" : loginUrl.Trim();
            var storedNotification = BuildNotification(
                homeowner,
                "Homeowner account created",
                "Your RestNestHome account has been created. Check your email or SMS for the login details.",
                "AccountCreated",
                "Homeowner",
                homeowner.HomeownerID);

            var emailBody =
                $"Hello {homeowner.FullName},{Environment.NewLine}{Environment.NewLine}" +
                "Your RestNestHome homeowner account has been created by the admin office." + Environment.NewLine +
                $"Login page: {normalizedLoginUrl}{Environment.NewLine}" +
                $"Email: {homeowner.Email}{Environment.NewLine}" +
                $"Temporary password: {temporaryPassword}{Environment.NewLine}{Environment.NewLine}" +
                "Please sign in and change your password after your first login.";

            var smsBody =
                $"RestNestHome account created. Email {homeowner.Email}. Temp password {temporaryPassword}. Sign in to the portal and change it after your first login.";

            await DeliverAsync(
                storedNotification,
                sendEmail: true,
                sendSms: true,
                cancellationToken,
                emailBodyOverride: emailBody,
                smsBodyOverride: smsBody);
        }

        public Task NotifyBillingSubmissionReviewedAsync(Billing billing, Homeowner homeowner, bool approved, CancellationToken cancellationToken = default)
        {
            var title = approved ? "Payment approved" : "Payment needs attention";
            var message = approved
                ? $"Your submitted payment for bill #{billing.BillingID} has been approved and your account has been updated."
                : $"Your submitted payment for bill #{billing.BillingID} was rejected. Please review the admin notes in the billing portal and resubmit if needed.";

            return DeliverAsync(
                BuildNotification(homeowner, title, message, "BillingReview", "Billing", billing.BillingID),
                sendEmail: true,
                sendSms: false,
                cancellationToken);
        }

        private Notification BuildNotification(Homeowner homeowner, string title, string message, string category, string relatedEntityType, int relatedEntityId)
        {
            return new Notification
            {
                Title = title,
                Message = message,
                Category = category,
                RecipientRole = "Homeowner",
                RecipientUserId = homeowner.HomeownerID,
                RecipientName = homeowner.FullName,
                RecipientEmail = homeowner.Email,
                RecipientPhoneNumber = homeowner.ContactNumber,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId
            };
        }

        private async Task DeliverAsync(
            Notification notification,
            bool sendEmail,
            bool sendSms,
            CancellationToken cancellationToken,
            string? emailBodyOverride = null,
            string? smsBodyOverride = null)
        {
            notification.EmailAttempted = sendEmail;
            notification.SmsAttempted = sendSms;

            if (sendEmail && !string.IsNullOrWhiteSpace(notification.RecipientEmail))
            {
                notification.EmailDelivered = await TrySendEmailAsync(notification, emailBodyOverride, cancellationToken);
            }

            if (sendSms && !string.IsNullOrWhiteSpace(notification.RecipientPhoneNumber))
            {
                notification.SmsDelivered = await TrySendSmsAsync(notification, smsBodyOverride, cancellationToken);
            }

            notification.DeliveryStatus = DetermineDeliveryStatus(notification);
            await _data.AddNotificationAsync(notification);
        }

        private async Task<bool> TrySendEmailAsync(Notification notification, string? bodyOverride, CancellationToken cancellationToken)
        {
            if (!_emailOptions.Enabled ||
                string.IsNullOrWhiteSpace(_emailOptions.SmtpHost) ||
                string.IsNullOrWhiteSpace(_emailOptions.FromAddress))
            {
                _logger.LogInformation("Email delivery skipped for notification '{Title}' because email is not configured.", notification.Title);
                return false;
            }

            try
            {
                using var client = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
                {
                    EnableSsl = _emailOptions.EnableSsl
                };

                if (!string.IsNullOrWhiteSpace(_emailOptions.Username))
                {
                    client.Credentials = new NetworkCredential(_emailOptions.Username, _emailOptions.Password);
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(_emailOptions.FromAddress, _emailOptions.FromName),
                    Subject = notification.Title,
                    Body = string.IsNullOrWhiteSpace(bodyOverride) ? notification.Message : bodyOverride,
                    IsBodyHtml = false
                };

                message.To.Add(notification.RecipientEmail!);
                using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
                await client.SendMailAsync(message, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email delivery failed for notification '{Title}' to {RecipientEmail}.", notification.Title, notification.RecipientEmail);
                return false;
            }
        }

        private async Task<bool> TrySendSmsAsync(Notification notification, string? bodyOverride, CancellationToken cancellationToken)
        {
            if (!_smsOptions.Enabled ||
                string.IsNullOrWhiteSpace(_smsOptions.ApiToken) ||
                string.IsNullOrWhiteSpace(_smsOptions.BaseUrl))
            {
                _logger.LogInformation("SMS delivery skipped for notification '{Title}' because SMS is not configured.", notification.Title);
                return false;
            }

            var destination = NormalizePhoneNumber(notification.RecipientPhoneNumber);
            if (string.IsNullOrWhiteSpace(destination))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient(nameof(CommunityNotificationService));
                var requestUri = $"{_smsOptions.BaseUrl.Trim()}?api_token={WebUtility.UrlEncode(_smsOptions.ApiToken.Trim())}";
                var postFields = new Dictionary<string, string>
                {
                    { "api_token", _smsOptions.ApiToken.Trim() },
                    { "phone_number", destination },
                    { "message", string.IsNullOrWhiteSpace(bodyOverride) ? notification.Message : bodyOverride }
                };

                if (_smsOptions.SmsProvider > 0)
                {
                    postFields["sms_provider"] = _smsOptions.SmsProvider.ToString();
                }

                using var response = await client.PostAsync(
                    requestUri,
                    new FormUrlEncodedContent(postFields),
                    cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "iProgSMS delivery failed for notification '{Title}' to {RecipientPhoneNumber}. StatusCode={StatusCode}, Response={Response}",
                        notification.Title,
                        destination,
                        (int)response.StatusCode,
                        responseBody);
                    return false;
                }

                try
                {
                    var json = JsonDocument.Parse(responseBody);
                    if (json.RootElement.TryGetProperty("message_id", out var messageIdProp))
                    {
                        var messageId = messageIdProp.GetString();
                        _logger.LogInformation(
                            "iProgSMS delivered notification '{Title}' to {RecipientPhoneNumber}. message_id={MessageId}",
                            notification.Title,
                            destination,
                            messageId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "iProgSMS delivered notification '{Title}' to {RecipientPhoneNumber}. Response={Response}",
                            notification.Title,
                            destination,
                            responseBody);
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "iProgSMS response parsed with warnings. Raw response: {Response}", responseBody);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS delivery failed for notification '{Title}' to {RecipientPhoneNumber}.", notification.Title, destination);
                return false;
            }
        }

        private string DetermineDeliveryStatus(Notification notification)
        {
            if ((notification.EmailAttempted && notification.EmailDelivered) ||
                (notification.SmsAttempted && notification.SmsDelivered))
            {
                return "Delivered";
            }

            if (notification.EmailAttempted || notification.SmsAttempted)
            {
                return "Stored";
            }

            return "InAppOnly";
        }

        private string? NormalizePhoneNumber(string? rawPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            {
                return null;
            }

            var trimmed = rawPhoneNumber.Trim();
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return null;
            }

            if (digits.StartsWith("0", StringComparison.Ordinal))
            {
                digits = digits[1..];
            }

            var countryCodeDigits = new string((_smsOptions.DefaultCountryCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(countryCodeDigits) && !digits.StartsWith(countryCodeDigits, StringComparison.Ordinal))
            {
                digits = $"{countryCodeDigits}{digits}";
            }

            return digits;
        }
    }
}
