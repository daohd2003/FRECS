using BusinessObject.DTOs.ApiResponses;
using BusinessObject.DTOs.Contact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.EmailRepositories;
using Services.EmailServices;

namespace ShareItAPI.Controllers
{
    [Route("api/contact")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IEmailRepository _emailRepository;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IEmailRepository emailRepository, ILogger<ContactController> logger)
        {
            _emailRepository = emailRepository;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormRequestDto request)
        {
            _logger.LogInformation("Contact form API endpoint was hit.");

            // === BƯỚC DEBUG QUAN TRỌNG: Kiểm tra ModelState ===
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid. Logging validation errors...");

                // Lấy tất cả các lỗi validation và ghi lại
                var errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                    );

                // Ghi từng lỗi ra console của API
                foreach (var error in errors)
                {
                    _logger.LogWarning("Validation Error for field '{Field}': {Errors}", error.Key, string.Join(", ", error.Value));
                }

                // Trả về lỗi 400 với chi tiết các lỗi để Frontend có thể xử lý nếu muốn
                return BadRequest(new ApiResponse<object>("Invalid data provided. Please check the errors.", errors));
            }

            // Nếu không có lỗi validation, tiếp tục gửi email như bình thường
            try
            {
                var adminEmail = "support@frecs.com";
                var subject = $"New Contact Form Submission: {request.Subject}";
                var body = $@"
                    <h3>You have a new contact message:</h3>
                    <ul>
                        <li><strong>Name:</strong> {request.Name}</li>
                        <li><strong>Email:</strong> {request.Email}</li>
                        <li><strong>Category:</strong> {request.Category ?? "N/A"}</li>
                        <li><strong>Subject:</strong> {request.Subject}</li>
                    </ul>
                    <hr>
                    <h4>Message:</h4>
                    <p>{request.Message}</p>";

                await _emailRepository.SendEmailAsync(adminEmail, subject, body);

                return Ok(new ApiResponse<string>("Your message has been sent successfully.", null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending contact form email.");
                return StatusCode(500, new ApiResponse<string>("An internal server error occurred.", null));
            }
        }
    }
}