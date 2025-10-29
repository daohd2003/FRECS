using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public EmailService(
            IEmailRepository emailRepository,
            IOptions<SmtpSettings> smtpSettings,
            IConfiguration configuration)
        {
            _emailRepository = emailRepository;
            _smtpSettings = smtpSettings.Value;
            _configuration = configuration;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            string subject = "Email Verification Required - FRECS Shop";
            string body = $@"
                <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f8fafc; padding: 20px;'>
                    <div style='background-color: white; border-radius: 16px; box-shadow: 0 10px 25px rgba(0,0,0,0.08); overflow: hidden;'>
                        
                        <!-- Header with brand -->
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 30px; text-align: center; position: relative;'>
                            <div style='background: rgba(255,255,255,0.2); width: 80px; height: 80px; border-radius: 50%; margin: 0 auto 20px; line-height: 80px; text-align: center; backdrop-filter: blur(10px); border: 2px solid rgba(255,255,255,0.3);'>
                                <img src='https://res.cloudinary.com/dhgxdjczg/image/upload/v1758434540/favicon-32x32_aegdwz.png' alt='FRECS' style='width: 40px; height: 40px; border-radius: 50%; vertical-align: middle;' />
                            </div>
                            <h1 style='color: white; margin: 0; font-size: 28px; font-weight: 700; letter-spacing: -0.5px;'>Welcome to FRECS!</h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0 0; font-size: 16px; font-weight: 400;'>Fashion Rental & E-commerce Platform</p>
                        </div>
                        
                        <!-- Main content -->
                        <div style='padding: 40px 30px;'>
                            <div style='text-align: center; margin-bottom: 32px;'>
                                <div style='background: #f0f9ff; width: 60px; height: 60px; border-radius: 50%; margin: 0 auto 20px; display: flex; align-items: center; justify-content: center;'>
                                    <svg width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""none"" stroke=""#0ea5e9"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                                        <path d=""M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z""></path>
                                    </svg>
                                </div>
                                <h2 style='color: #1e293b; margin: 0 0 12px 0; font-size: 24px; font-weight: 600;'>Verify Your Email Address</h2>
                                <p style='color: #64748b; margin: 0; font-size: 16px; line-height: 1.6;'>
                                    Thank you for joining FRECS! To complete your registration and start exploring our fashion collection, 
                                    please verify your email address.
                                </p>
                            </div>
                            
                            <!-- CTA Button -->
                            <div style='text-align: center; margin: 40px 0;'>
                                <a href='{verificationLink}' 
                                   style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; 
                                          padding: 16px 32px; text-decoration: none; border-radius: 12px; font-weight: 600; 
                                          font-size: 16px; box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4); 
                                          transition: all 0.3s ease; border: none; cursor: pointer;'>
                                    <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" style=""margin-right: 8px; vertical-align: middle;"">
                                        <path d=""M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z""></path>
                                    </svg>
                                    Verify Email Address
                                </a>
                            </div>
                            
                            <!-- Info box -->
                            <div style='background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%); border: 1px solid #bae6fd; 
                                        border-radius: 12px; padding: 20px; margin: 30px 0;'>
                                <div style='display: flex; align-items: flex-start; gap: 12px;'>
                                    <div style='width: 24px; height: 24px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; margin-top: 2px;'>
                                        <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" stroke=""#0ea5e9"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                                            <circle cx=""12"" cy=""12"" r=""10""></circle>
                                            <line x1=""12"" y1=""16"" x2=""12"" y2=""12""></line>
                                            <line x1=""12"" y1=""8"" x2=""12.01"" y2=""8""></line>
                                        </svg>
                                    </div>
                                    <div>
                                        <p style='margin: 0 0 8px 0; color: #075985; font-weight: 600; font-size: 14px;'>Important Security Information</p>
                                        <p style='margin: 0; color: #0369a1; font-size: 14px; line-height: 1.5;'>
                                            This verification link will expire in <strong>1 hour</strong> for your security. 
                                            After verification, you'll have full access to browse, rent, and purchase from our collection.
                                        </p>
                                    </div>
                                </div>
                            </div>
                            
                            <!-- Alternative link -->
                            <div style='background-color: #f8fafc; border-radius: 8px; padding: 20px; text-align: center; margin: 30px 0;'>
                                <p style='margin: 0 0 8px 0; color: #64748b; font-size: 14px;'>
                                    Having trouble with the button? Copy and paste this link into your browser:
                                </p>
                                <p style='margin: 0; word-break: break-all;'>
                                    <a href='{verificationLink}' style='color: #667eea; text-decoration: none; font-size: 13px;'>{verificationLink}</a>
                                </p>
                            </div>
                        </div>
                        
                        <!-- Footer -->
                        <div style='background-color: #f8fafc; padding: 30px; text-align: center; border-top: 1px solid #e2e8f0;'>
                            <div style='margin-bottom: 20px;'>
                                <div style='display: inline-flex; align-items: center; gap: 8px; color: #667eea; font-weight: 600; font-size: 18px;'>
                                    <svg width=""24"" height=""24"" viewBox=""0 0 24 24"" fill=""#667eea"" stroke=""none"">
                                        <path d=""M6.7 16.2l-2.8 2.8a1 1 0 0 0 0 1.4l7.1 7.1a1 1 0 0 0 1.4 0l7.1-7.1a1 1 0 0 0 0-1.4l-2.8-2.8""></path>
                                        <path d=""M8.1 14.8L3 20H21l-5.1-5.2""></path>
                                        <path d=""M12 2a4 4 0 0 0-4 4v2.4a4 4 0 0 0 8 0V6A4 4 0 0 0 12 2Z""></path>
                                        <line x1=""10"" y1=""12"" x2=""14"" y2=""12"" stroke=""white"" stroke-width=""1.5""></line>
                                    </svg>
                                    FRECS
                                </div>
                            </div>
                            <p style='margin: 0 0 8px 0; color: #64748b; font-size: 13px;'>
                                Didn't create an account? Please ignore this email.
                            </p>
                            <p style='margin: 0; color: #94a3b8; font-size: 12px;'>
                                © {DateTime.Now.Year} FRECS Shop - Fashion Rental & E-commerce Platform
                            </p>
                        </div>
                    </div>
                </div>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            string subject = "🔑 Password Reset Request - FRECS Shop";
            string body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f8f9fa; padding: 20px;'>
                    <div style='background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                        <div style='text-align: center; margin-bottom: 30px;'>
                            <h2 style='color: #dc3545; margin: 0;'>🔐 Password Reset Request</h2>
                        </div>
                        
                        <div style='background: linear-gradient(135deg, #ff6b6b 0%, #ee5a52 100%); padding: 20px; border-radius: 8px; color: white; text-align: center; margin-bottom: 30px;'>
                            <h3 style='margin: 0 0 10px 0;'>🔒 Reset Your Password</h3>
                            <p style='margin: 0; font-size: 16px;'>We received a request to reset your password</p>
                        </div>
                        
                        <div style='margin: 20px 0;'>
                            <p style='color: #374151; font-size: 16px; line-height: 1.6;'>
                                Someone requested a password reset for your FRECS Shop account. If this was you, 
                                click the button below to set a new password:
                            </p>
                        </div>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' 
                               style='display: inline-block; background: #dc3545; color: white; padding: 15px 30px; 
                                      text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; 
                                      box-shadow: 0 4px 15px rgba(220, 53, 69, 0.3); transition: all 0.3s;'>
                                🔑 Reset My Password
                            </a>
                        </div>
                        
                        <div style='background-color: #fef2f2; padding: 15px; border-radius: 8px; border-left: 4px solid #ef4444;'>
                            <p style='margin: 0 0 10px 0; font-size: 14px; color: #dc2626;'>
                                <strong>⏰ Important:</strong> This reset link will expire in 30 minutes for security.
                            </p>
                            <p style='margin: 0; font-size: 14px; color: #dc2626;'>
                                <strong>🚨 Security Alert:</strong> If you didn't request this, please ignore this email and consider changing your password.
                            </p>
                        </div>
                        
                        <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0;'>
                        
                        <div style='text-align: center; color: #64748b; font-size: 13px;'>
                            <p>Need help? Contact our support team.</p>
                            <p>© {DateTime.Now.Year} FRECS Shop - Fashion Rental & E-commerce</p>
                        </div>
                    </div>
                </div>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendBanNotificationEmailAsync(string toEmail, string reason)
        {
            string subject = "Account Ban Notification - FRECS Shop";
            string body = $@"
                <h3>Your FRECS Shop account has been banned</h3>
                <p>Reason: <strong>{reason}</strong></p>
                <p>If you believe this is a mistake, please contact our support team.</p>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }
        public async Task SendContactFormEmailAsync(ContactFormRequestDto formData)
        {
            var adminEmail = "support@frecs.com";
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

      public async Task SendProviderApplicationApprovedEmailAsync(string toEmail, string businessName)
{
    string subject = "🎉 Welcome to FRECS! Your Provider Application is Approved";
    string body = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <style>
                @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap');
            </style>
        </head>
        <body style='margin: 0; padding: 0; width: 100%; background-color: #f0fdf4; font-family: ""Inter"", -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif;'>
            <div style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.08);'>
                
                <div style='padding: 30px 40px; text-align: center; background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%);'>
                    <h1 style='margin: 0; color: #059669; font-size: 32px; font-weight: 800; letter-spacing: -0.8px;'>
                        FRECS
                    </h1>
                </div>

                <div style='padding: 40px;'>
                    <h2 style='color: #1e293b; font-size: 26px; font-weight: 700; margin-top: 0; margin-bottom: 16px;'>
                        Welcome to FRECS, {businessName}!
                    </h2>
                    <p style='color: #475569; font-size: 16px; line-height: 1.6; margin: 0 0 30px 0;'>
                        We're thrilled to let you know that your application to become a provider has been <strong style='color: #059669;'>approved</strong>. Congratulations on officially joining the FRECS community!
                    </p>

                    <div style='background-color: #ecfdf5; border-left: 5px solid #10b981; padding: 20px; margin-bottom: 30px; border-radius: 0 8px 8px 0; box-shadow: 0 2px 8px rgba(0,0,0,0.03);'>
                        <p style='margin: 0; color: #065f46; font-size: 15px; line-height: 1.6;'>
                            <strong>Action Required:</strong> Please <strong>log out and log back in</strong> to access your new provider features and dashboard.
                        </p>
                    </div>

                    <h3 style='color: #1e293b; font-size: 19px; font-weight: 600; margin-bottom: 16px;'>
                        You can now get started with:
                    </h3>
                    <ul style='list-style-type: none; padding: 0; margin: 0 0 30px 0; color: #475569;'>
                        <li style='margin-bottom: 12px; display: flex; align-items: center;'><span style='margin-right: 12px; font-size: 20px; color: #10b981;'>📦</span>List unlimited products.</li>
                        <li style='margin-bottom: 12px; display: flex; align-items: center;'><span style='margin-right: 12px; font-size: 20px; color: #10b981;'>📊</span>Access your advanced analytics dashboard.</li>
                        <li style='margin-bottom: 12px; display: flex; align-items: center;'><span style='margin-right: 12px; font-size: 20px; color: #10b981;'>💰</span>Earn revenue from rentals and sales.</li>
                        <li style='display: flex; align-items: center;'><span style='margin-right: 12px; font-size: 20px; color: #10b981;'>⚡</span>Manage orders and inventory efficiently.</li>
                    </ul>

                    <div style='text-align: center; margin: 40px 0;'>
                        <a href='{GetFrontendBaseUrl()}/Auth' style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: #ffffff; text-decoration: none; padding: 16px 32px; border-radius: 8px; font-weight: 600; font-size: 16px; display: inline-block; box-shadow: 0 8px 20px rgba(16, 185, 129, 0.3);'>
                            Login to Your Dashboard
                        </a>
                    </div>

                    <p style='color: #475569; font-size: 16px; line-height: 1.6; text-align: center; margin-top: 30px;'>
                        If you have any questions, feel free to <a href='mailto:support@frecs.com' style='color: #059669; text-decoration: none; font-weight: 600;'>contact our support team</a>.
                    </p>
                </div>

                <div style='background-color: #1e293b; padding: 30px; text-align: center; border-top: 5px solid #059669;'>
                    <p style='color: #94a3b8; font-size: 12px; margin: 0;'>
                        © 2025 FRECS. All rights reserved.<br>
                        You received this email because you applied to become a provider on FRECS.
                    </p>
                </div>
            </div>
        </body>
        </html>";

    await _emailRepository.SendEmailAsync(toEmail, subject, body);
}

        public async Task SendProviderApplicationRejectedEmailAsync(string toEmail, string businessName, string rejectionReason)
{
    string subject = "An Update on Your FRECS Provider Application";
    string body = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <style>
                @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap');
            </style>
        </head>
        <body style='margin: 0; padding: 0; width: 100%; background-color: #fffbeb; font-family: ""Inter"", -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif;'>
            <div style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.08);'>
                
                <div style='padding: 30px 40px; text-align: center; background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);'>
                    <h1 style='margin: 0; color: #a16207; font-size: 32px; font-weight: 800; letter-spacing: -0.8px;'>
                        FRECS
                    </h1>
                </div>

                <div style='padding: 40px;'>
                    <h2 style='color: #1e293b; font-size: 26px; font-weight: 700; margin-top: 0; margin-bottom: 16px;'>
                        Update on Your Application for {businessName}
                    </h2>
                    <p style='color: #475569; font-size: 16px; line-height: 1.6; margin: 0 0 30px 0;'>
                        Thank you for your interest in becoming a provider on FRECS. After a careful review, we regret to inform you that your application could not be approved at this time.
                    </p>

                    <div style='background-color: #fefce8; border-left: 5px solid #f59e0b; padding: 20px; margin: 30px 0; border-radius: 0 8px 8px 0; box-shadow: 0 2px 8px rgba(0,0,0,0.03);'>
                        <p style='margin: 0 0 8px 0; color: #92400e; font-size: 15px; font-weight: 600;'>Reason for Decision:</p>
                        <p style='margin: 0; color: #b45309; font-size: 15px; line-height: 1.6;'>
                            {rejectionReason}
                        </p>
                    </div>

                    <h3 style='color: #1e293b; font-size: 19px; font-weight: 600; margin-bottom: 16px;'>
                        Next Steps
                    </h3>
                    <p style='color: #475569; font-size: 16px; line-height: 1.6; margin-bottom: 30px; text-align: center;'>
                        While this application cannot be reconsidered, you are welcome to submit a new application once you have addressed the concerns noted above.
                    </p>
                    
                    <div style='text-align: center; margin: 40px 0;'>
                        <a href='mailto:support@frecs.com' style='background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: #ffffff; text-decoration: none; padding: 16px 32px; border-radius: 8px; font-weight: 600; font-size: 16px; display: inline-block; box-shadow: 0 8px 20px rgba(245, 158, 11, 0.3);'>
                            Contact Support
                        </a>
                         <a href='{GetFrontendBaseUrl()}' style='margin-left: 12px; color: #334155; text-decoration: none; padding: 16px 32px; font-weight: 600; font-size: 16px; display: inline-block; border: 1px solid #cbd5e1; border-radius: 8px;'>
                            Return to Homepage
                        </a>
                    </div>
                </div>

                <div style='background-color: #1e293b; padding: 30px; text-align: center; border-top: 5px solid #f59e0b;'>
                    <p style='color: #94a3b8; font-size: 12px; margin: 0;'>
                        © 2025 FRECS. All rights reserved.<br>
                        You received this email regarding your provider application on FRECS.
                    </p>
                </div>
            </div>
        </body>
        </html>";

    await _emailRepository.SendEmailAsync(toEmail, subject, body);
}


        public async Task SendProductModerationReviewEmailAsync(string toEmail, string productName, string reason, List<string> violatedTerms)
        {
            string subject = "Product Needs Review - Content Moderation Alert | FRECS";
            string violatedTermsList = string.Join(", ", violatedTerms ?? new List<string> { "Not specified" });
            
            string body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin: 0; padding: 0; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;'>
                    <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f8fafc; padding: 20px;'>
                        <div style='background-color: white; border-radius: 16px; box-shadow: 0 10px 25px rgba(0,0,0,0.08); overflow: hidden;'>
                            
                            <!-- Header with brand -->
                            <div style='background: linear-gradient(135deg, #f59e0b 0%, #dc2626 100%); padding: 40px 30px; text-align: center; position: relative;'>
                                <div style='background: rgba(255,255,255,0.2); width: 80px; height: 80px; border-radius: 50%; margin: 0 auto 20px; line-height: 80px; text-align: center; backdrop-filter: blur(10px); border: 2px solid rgba(255,255,255,0.3);'>
                                    <img src='https://res.cloudinary.com/dhgxdjczg/image/upload/v1758434540/favicon-32x32_aegdwz.png' alt='FRECS' style='width: 40px; height: 40px; border-radius: 50%; vertical-align: middle;' />
                                </div>
                                <h1 style='color: white; margin: 0; font-size: 28px; font-weight: 700; letter-spacing: -0.5px;'>Product Review Required</h1>
                                <p style='color: rgba(255,255,255,0.9); margin: 8px 0 0 0; font-size: 16px; font-weight: 400;'>Content Moderation Alert</p>
                            </div>
                            
                            <!-- Main content -->
                            <div style='padding: 40px 30px;'>
                                <div style='text-align: center; margin-bottom: 32px;'>
                                    <div style='background: #fef3c7; width: 60px; height: 60px; border-radius: 50%; margin: 0 auto 20px; display: flex; align-items: center; justify-content: center; font-size: 32px;'>
                                        ⚠️
                                    </div>
                                    <h2 style='color: #1e293b; margin: 0 0 12px 0; font-size: 24px; font-weight: 600;'>Action Required: Edit Your Product</h2>
                                    <p style='color: #64748b; font-size: 15px; line-height: 1.6; margin: 0;'>
                                        Your product listing has been flagged by our automated content moderation system and has been moved to PENDING status. It will be hidden from customers until you make the necessary edits.
                                    </p>
                                </div>
                                
                                <!-- Product Info -->
                                <div style='background: #fef3c7; border-left: 4px solid #f59e0b; padding: 20px; border-radius: 8px; margin-bottom: 24px;'>
                                    <h3 style='color: #92400e; margin: 0 0 8px 0; font-size: 14px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px;'>Product Name</h3>
                                    <p style='color: #78350f; font-size: 18px; font-weight: 600; margin: 0 0 16px 0; word-break: break-word;'>{productName}</p>
                                    
                                    <h3 style='color: #92400e; margin: 0 0 8px 0; font-size: 14px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px;'>Issue Detected</h3>
                                    <p style='color: #78350f; font-size: 15px; margin: 0 0 16px 0; line-height: 1.5;'>{reason}</p>
                                    
                                    <h3 style='color: #92400e; margin: 0 0 8px 0; font-size: 14px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px;'>Flagged Terms</h3>
                                    <p style='color: #78350f; font-size: 14px; margin: 0; line-height: 1.5; background: rgba(255,255,255,0.6); padding: 12px; border-radius: 6px; word-break: break-word;'>
                                        {violatedTermsList}
                                    </p>
                                </div>
                                
                                <!-- What Happens Next -->
                                <div style='background: #f1f5f9; padding: 20px; border-radius: 8px; margin-bottom: 24px;'>
                                    <h3 style='color: #1e293b; margin: 0 0 16px 0; font-size: 16px; font-weight: 600;'>What Happens Next?</h3>
                                    <div style='color: #475569; font-size: 14px; line-height: 1.7;'>
                                        <p style='margin: 0 0 12px 0;'><strong style='color: #1e293b;'>⏸️ Your product has been moved to PENDING:</strong><br/>It was briefly visible but is now hidden from customers until you make the necessary edits.</p>
                                        <p style='margin: 0 0 12px 0;'><strong style='color: #1e293b;'>✏️ Edit your product:</strong><br/>Review the flagged content and update your product name or description to comply with our community guidelines.</p>
                                        <p style='margin: 0;'><strong style='color: #1e293b;'>✅ Automatic re-check:</strong><br/>Once you save your edits, our system will automatically re-evaluate your product. If it passes, it will be published immediately.</p>
                                    </div>
                                </div>
                                
                                <!-- Community Guidelines -->
                                <div style='background: #eff6ff; border-left: 4px solid #3b82f6; padding: 16px; border-radius: 8px; margin-bottom: 24px;'>
                                    <h4 style='color: #1e40af; margin: 0 0 12px 0; font-size: 14px; font-weight: 600;'>📋 Our Community Guidelines</h4>
                                    <div style='color: #1e40af; font-size: 13px; line-height: 1.6;'>
                                        Products must not contain:<br/>
                                        • Offensive language or profanity<br/>
                                        • Hate speech or discrimination<br/>
                                        • Sexual/adult content<br/>
                                        • Scam or fraud indicators<br/>
                                        • Spam or low-quality content<br/>
                                        • Counterfeit brands (e.g., ""fake"", ""replica"")
                                    </div>
                                </div>
                                
                                <!-- CTA Button -->
                                <div style='text-align: center; margin: 24px 0;'>
                                    <a href='{GetFrontendBaseUrl()}/Provider/Products' style='display: inline-block; background: linear-gradient(135deg, #f59e0b 0%, #dc2626 100%); color: white; text-decoration: none; padding: 14px 36px; border-radius: 8px; font-weight: 700; font-size: 15px; box-shadow: 0 4px 12px rgba(245, 158, 11, 0.3);'>
                                        ✏️ Edit My Product Now
                                    </a>
                                </div>
                                
                                <!-- Support -->
                                <p style='color: #64748b; font-size: 12px; text-align: center; margin: 16px 0 0 0; line-height: 1.6;'>
                                    If you believe this is an error or have questions, please <a href='mailto:support@frecs.com' style='color: #3b82f6; text-decoration: none; font-weight: 600;'>contact our support team</a>.
                                </p>
                            </div>
                            
                            <!-- Footer -->
                            <div style='background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 32px 30px; text-align: center;'>
                                <p style='color: #e2e8f0; font-size: 14px; margin: 0 0 12px 0; font-weight: 600;'>
                                    FRECS - Fashion Rental & E-commerce Platform
                                </p>
                                <p style='color: #94a3b8; font-size: 12px; margin: 0 0 16px 0;'>
                                    © 2025 FRECS Shop. All rights reserved.
                                </p>
                                <div style='padding-top: 16px; border-top: 1px solid rgba(255,255,255,0.1);'>
                                    <p style='color: #64748b; font-size: 11px; margin: 0; line-height: 1.6;'>
                                        This email was sent to {toEmail} regarding your product listing on FRECS.<br/>
                                        This is an automated message from our content moderation system.
                                    </p>
                                </div>
                            </div>
                        </div>
                        
                        <!-- Bottom Spacing -->
                        <div style='height: 12px;'></div>
                    </div>
                </body>
                </html>";

            await _emailRepository.SendEmailAsync(toEmail, subject, body);
        }

        private string GetFrontendBaseUrl()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
            var baseUrl = _configuration[$"FrontendSettings:{environment}:BaseUrl"] ?? "https://localhost:7045";
            return baseUrl;
        }
    }
}