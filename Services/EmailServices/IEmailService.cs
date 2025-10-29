using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessObject.DTOs.Contact;

namespace Services.EmailServices
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verificationLink);
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
        Task SendBanNotificationEmailAsync(string toEmail, string reason);
        Task SendContactFormEmailAsync(ContactFormRequestDto formData);
        Task SendProviderApplicationApprovedEmailAsync(string toEmail, string businessName);
        Task SendProviderApplicationRejectedEmailAsync(string toEmail, string businessName, string rejectionReason);
        Task SendProductModerationReviewEmailAsync(string toEmail, string productName, string reason, List<string> violatedTerms);
    }
}
