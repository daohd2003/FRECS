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
            string subject = "🎉 Provider Application Approved - Welcome to FRECS!";
            string body = $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin: 0; padding: 0; background-color: #f1f5f9;'>
                    <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f1f5f9; padding: 12px;'>
                        
                        <!-- Logo Bar -->
                        <div style='text-align: center; padding: 12px 0;'>
                            <div style='display: inline-block; background: white; padding: 8px 20px; border-radius: 50px; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>
                                <h2 style='margin: 0; color: #10b981; font-size: 20px; font-weight: 800; letter-spacing: -0.5px;'>
                                    <span style='color: #059669;'>F</span>RECS
                                </h2>
                            </div>
                        </div>

                        <div style='background-color: white; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.08); overflow: hidden;'>
                            
                            <!-- Celebration Header -->
                            <div style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 20px 24px; text-align: center; position: relative; overflow: hidden;'>
                                <div style='position: absolute; top: -20px; right: -20px; width: 80px; height: 80px; background: rgba(255,255,255,0.06); border-radius: 50%;'></div>
                                <div style='position: absolute; bottom: -25px; left: -25px; width: 100px; height: 100px; background: rgba(255,255,255,0.06); border-radius: 50%;'></div>
                                
                                <div style='position: relative; z-index: 1;'>
                                    <div style='background: rgba(255,255,255,0.25); width: 50px; height: 50px; border-radius: 50%; margin: 0 auto 12px; display: flex; align-items: center; justify-content: center; backdrop-filter: blur(10px); border: 2px solid rgba(255,255,255,0.4);'>
                                        <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white' style='width: 28px; height: 28px;'>
                                            <path fill-rule='evenodd' d='M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12zm13.36-1.814a.75.75 0 10-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 00-1.06 1.06l2.25 2.25a.75.75 0 001.14-.094l3.75-5.25z' clip-rule='evenodd'/>
                                        </svg>
                                    </div>
                                    <h1 style='color: white; margin: 0 0 6px 0; font-size: 24px; font-weight: 800; text-shadow: 0 2px 4px rgba(0,0,0,0.1);'>Application Approved!</h1>
                                    <p style='color: rgba(255,255,255,0.95); margin: 0; font-size: 14px; font-weight: 500;'>🎉 Congratulations, {businessName}!</p>
                                </div>
                            </div>
                            
                            <!-- Main Content -->
                            <div style='padding: 20px 24px;'>
                                <p style='color: #475569; font-size: 14px; line-height: 1.5; margin: 0 0 16px 0;'>
                                    Dear <strong style='color: #10b981;'>{businessName}</strong>, your provider application has been <strong style='color: #10b981;'>approved</strong>! Welcome to FRECS! 🌟
                                </p>
                                
                                <!-- Important Notice -->
                                <div style='background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%); border-left: 3px solid #3b82f6; border-radius: 8px; padding: 14px; margin: 16px 0;'>
                                    <div style='display: flex; align-items: start;'>
                                        <div style='font-size: 20px; margin-right: 8px;'>⚠️</div>
                                        <div>
                                            <h3 style='color: #1e40af; margin: 0 0 6px 0; font-size: 14px; font-weight: 700;'>Action Required: Login Again</h3>
                                            <p style='color: #1e3a8a; font-size: 13px; margin: 0; line-height: 1.5;'>
                                                Your account is now <strong>Provider</strong>. Please <strong>logout and login</strong> to access provider features.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                                
                                <!-- Features -->
                                <div style='background: linear-gradient(135deg, #ecfdf5 0%, #d1fae5 100%); border-radius: 8px; padding: 14px; margin: 16px 0;'>
                                    <p style='color: #047857; font-size: 13px; margin: 0 0 10px 0; font-weight: 600; text-align: center;'>🚀 Your Provider Benefits</p>
                                    <div style='color: #059669; font-size: 12px; line-height: 1.6;'>
                                        • List products on marketplace<br/>
                                        • Access analytics dashboard<br/>
                                        • Earn from rentals<br/>
                                        • Manage orders & inventory
                                    </div>
                                </div>
                                
                                <!-- CTA Button -->
                                <div style='text-align: center; margin: 18px 0 12px 0;'>
                                    <a href='{GetFrontendBaseUrl()}/Auth' style='display: inline-block; background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: white; text-decoration: none; padding: 12px 32px; border-radius: 8px; font-weight: 700; font-size: 14px; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.25);'>
                                        🔐 Login Now
                                    </a>
                                </div>
                                
                                <!-- Support -->
                                <p style='color: #64748b; font-size: 11px; text-align: center; margin: 0;'>
                                    Need help? <a href='mailto:support@frecs.com' style='color: #10b981; text-decoration: none; font-weight: 600;'>Contact Support</a>
                                </p>
                            </div>
                            
                            <!-- Footer -->
                            <div style='background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 16px 24px; text-align: center;'>
                                <p style='color: #e2e8f0; font-size: 12px; margin: 0 0 6px 0; font-weight: 600;'>
                                    FRECS - Fashion Rental & E-commerce Platform
                                </p>
                                <p style='color: #94a3b8; font-size: 10px; margin: 0 0 10px 0;'>
                                    © 2025 FRECS Shop. All rights reserved.
                                </p>
                                <div style='padding-top: 10px; border-top: 1px solid rgba(255,255,255,0.1);'>
                                    <p style='color: #64748b; font-size: 9px; margin: 0; line-height: 1.4;'>
                                        This email was sent to {toEmail} because you applied to become a provider on FRECS.
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

        public async Task SendProviderApplicationRejectedEmailAsync(string toEmail, string businessName, string rejectionReason)
        {
            string subject = "Provider Application Update - FRECS Shop";
            string body = $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin: 0; padding: 0; background-color: #f1f5f9;'>
                    <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f1f5f9; padding: 12px;'>
                        
                        <!-- Logo Bar -->
                        <div style='text-align: center; padding: 12px 0;'>
                            <div style='display: inline-block; background: white; padding: 8px 20px; border-radius: 50px; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>
                                <h2 style='margin: 0; color: #10b981; font-size: 20px; font-weight: 800; letter-spacing: -0.5px;'>
                                    <span style='color: #059669;'>F</span>RECS
                                </h2>
                            </div>
                        </div>

                        <div style='background-color: white; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.08); overflow: hidden;'>
                            
                            <!-- Header -->
                            <div style='background: linear-gradient(135deg, #f97316 0%, #ea580c 100%); padding: 20px 24px; text-align: center; position: relative; overflow: hidden;'>
                                <div style='position: absolute; top: -20px; right: -20px; width: 80px; height: 80px; background: rgba(255,255,255,0.06); border-radius: 50%;'></div>
                                <div style='position: absolute; bottom: -25px; left: -25px; width: 100px; height: 100px; background: rgba(255,255,255,0.06); border-radius: 50%;'></div>
                                
                                <div style='position: relative; z-index: 1;'>
                                    <div style='background: rgba(255,255,255,0.25); width: 50px; height: 50px; border-radius: 50%; margin: 0 auto 12px; display: flex; align-items: center; justify-content: center; backdrop-filter: blur(10px); border: 2px solid rgba(255,255,255,0.4);'>
                                        <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white' style='width: 28px; height: 28px;'>
                                            <path fill-rule='evenodd' d='M9.401 3.003c1.155-2 4.043-2 5.197 0l7.355 12.748c1.154 2-.29 4.5-2.599 4.5H4.645c-2.309 0-3.752-2.5-2.598-4.5L9.4 3.003zM12 8.25a.75.75 0 01.75.75v3.75a.75.75 0 01-1.5 0V9a.75.75 0 01.75-.75zm0 8.25a.75.75 0 100-1.5.75.75 0 000 1.5z' clip-rule='evenodd'/>
                                        </svg>
                                    </div>
                                    <h1 style='color: white; margin: 0 0 6px 0; font-size: 24px; font-weight: 800; text-shadow: 0 2px 4px rgba(0,0,0,0.1);'>Application Update</h1>
                                    <p style='color: rgba(255,255,255,0.95); margin: 0; font-size: 14px; font-weight: 500;'>Provider Application Decision</p>
                                </div>
                            </div>
                            
                            <!-- Main Content -->
                            <div style='padding: 20px 24px;'>
                                <p style='color: #475569; font-size: 14px; line-height: 1.5; margin: 0 0 16px 0;'>
                                    Dear <strong style='color: #f97316;'>{businessName}</strong>, thank you for your interest. After review, we are unable to approve your application at this time.
                                </p>
                                
                                <!-- Rejection Reason -->
                                <div style='background: linear-gradient(135deg, #fef2f2 0%, #fee2e2 100%); border-left: 3px solid #ef4444; border-radius: 8px; padding: 14px; margin: 16px 0;'>
                                    <div style='display: flex; align-items: start;'>
                                        <div style='font-size: 20px; margin-right: 8px;'>📋</div>
                                        <div>
                                            <h3 style='color: #991b1b; margin: 0 0 6px 0; font-size: 14px; font-weight: 700;'>Reason</h3>
                                            <p style='color: #7f1d1d; font-size: 13px; margin: 0; line-height: 1.5; background: white; padding: 12px; border-radius: 6px;'>
                                                {rejectionReason}
                                            </p>
                                        </div>
                                    </div>
                                </div>
                                
                                <!-- Important Information -->
                                <div style='background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%); border-left: 3px solid #f59e0b; border-radius: 8px; padding: 14px; margin: 16px 0;'>
                                    <div style='display: flex; align-items: start;'>
                                        <div style='font-size: 20px; margin-right: 8px;'>⚠️</div>
                                        <div>
                                            <h3 style='color: #92400e; margin: 0 0 6px 0; font-size: 14px; font-weight: 700;'>Important</h3>
                                            <p style='color: #78350f; font-size: 13px; margin: 0; line-height: 1.5;'>
                                                Rejected applications <strong>cannot be re-approved</strong>. You may submit a <strong>new application</strong> addressing the concerns above.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                                
                                <!-- Next Steps -->
                                <div style='background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%); border-radius: 8px; padding: 14px; margin: 16px 0;'>
                                    <p style='color: #0369a1; font-size: 13px; margin: 0 0 10px 0; font-weight: 600; text-align: center;'>💡 What You Can Do</p>
                                    <div style='color: #0284c7; font-size: 12px; line-height: 1.6;'>
                                        • Review feedback carefully<br/>
                                        • Address the concerns<br/>
                                        • Submit new application when ready<br/>
                                        • Continue shopping as customer
                                    </div>
                                </div>
                                
                                <!-- CTA Button -->
                                <div style='text-align: center; margin: 18px 0 12px 0;'>
                                    <a href='{GetFrontendBaseUrl()}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; padding: 12px 32px; border-radius: 8px; font-weight: 700; font-size: 14px; box-shadow: 0 4px 12px rgba(102, 126, 234, 0.25);'>
                                        🏠 Return to FRECS
                                    </a>
                                </div>
                                
                                <!-- Support -->
                                <p style='color: #64748b; font-size: 11px; text-align: center; margin: 0;'>
                                    Questions? <a href='mailto:support@frecs.com' style='color: #10b981; text-decoration: none; font-weight: 600;'>Contact Support</a>
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
                                        This email was sent to {toEmail} regarding your provider application on FRECS.
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