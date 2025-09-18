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
            string subject = "Email Verification Required - FRECS Shop";
            string body = $@"
                <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f8fafc; padding: 20px;'>
                    <div style='background-color: white; border-radius: 16px; box-shadow: 0 10px 25px rgba(0,0,0,0.08); overflow: hidden;'>
                        
                        <!-- Header with brand -->
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 40px 30px; text-align: center; position: relative;'>
                            <div style='background: rgba(255,255,255,0.2); width: 80px; height: 80px; border-radius: 50%; margin: 0 auto 20px; display: table; backdrop-filter: blur(10px); border: 2px solid rgba(255,255,255,0.3);'>
                                <div style='display: table-cell; vertical-align: middle; text-align: center; color: white; font-size: 32px; font-weight: 800; letter-spacing: -1px; line-height: 1;'>
                                    F
                                </div>
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
                                    <div style='background: #0ea5e9; width: 24px; height: 24px; border-radius: 50%; 
                                                display: flex; align-items: center; justify-content: center; flex-shrink: 0; margin-top: 2px;'>
                                        <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" stroke=""white"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
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
    }
}