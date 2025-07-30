using Microsoft.Extensions.Options;
using Repositories.EmailRepositories;
using BusinessObject.DTOs.EmailSetiings;
using System.Threading.Tasks;
using BusinessObject.DTOs.Contact;

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
        public async Task SendContactFormEmailAsync(ContactFormRequestDto formData)
        {
            var adminEmail = "support@rentchic.com";
            var subject = $"New Contact Form Submission: {formData.Subject}";

            var body = $@"
            <h3>You have a new contact message from your website:</h3>
            <ul>
                <li><strong>Name:</strong> {formData.Name}</li>
                <li><strong>Email:</strong> {formData.Email}</li>
                <li><strong>Category:</strong> {formData.Category ?? "Not specified"}</li>
                <li><strong>Subject:</strong> {formData.Subject}</li>
            </ul>
            <hr>
            <h4>Message:</h4>
            <p style='white-space: pre-wrap;'>{formData.Message}</p>
            <hr>
            <p><i>Please reply to the sender's email directly: <a href='mailto:{formData.Email}'>{formData.Email}</a></i></p>";

            await _emailRepository.SendEmailAsync(adminEmail, subject, body);
        }
    }
}