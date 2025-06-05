using Microsoft.Extensions.Options;
using Repositories.EmailRepositories;
using BusinessObject.DTOs.EmailSetiings;
using System.Threading.Tasks;

namespace Services.EmailServices
{
    public class EmailService : IEmailService
    {
        private readonly IEmailRepository _emailRepository;
        private readonly SmtpSettings _smtpSettings;

        public EmailService(
            IEmailRepository emailRepository,
            IOptions<SmtpSettings> smtpSettings)
        {
            _emailRepository = emailRepository;
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            string subject = "Email Verification - ShareIT Shop";
            string body = $@"
                <h3>Welcome to ShareIT Shop!</h3>
                <p>Please verify your email by clicking the link below:</p>
                <p><a href='{verificationLink}'>Verify Email</a></p>
                <br />
                <p>If you didn't create an account, please ignore this email.</p>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendBanNotificationEmailAsync(string toEmail, string reason)
        {
            string subject = "Account Ban Notification - ShareIT Shop";
            string body = $@"
                <h3>Your ShareIT Shop account has been banned</h3>
                <p>Reason: <strong>{reason}</strong></p>
                <p>If you believe this is a mistake, please contact our support team.</p>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }
    }
}