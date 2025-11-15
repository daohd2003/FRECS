using BusinessObject.DTOs.AIDtos;
using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AI;

namespace ShareItAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EkycController : ControllerBase
    {
        private readonly IEkycService _ekycService;
        private readonly IFaceMatchService _faceMatchService;

        public EkycController(IEkycService ekycService, IFaceMatchService faceMatchService)
        {
            _ekycService = ekycService;
            _faceMatchService = faceMatchService;
        }

        /// <summary>
        /// Verify a single side of CCCD (front or back)
        /// </summary>
        /// <param name="request">Request containing CCCD image file</param>
        /// <returns>Verification result with extracted data</returns>
        [HttpPost("verify-single")]
        [Consumes("multipart/form-data")]
        // [Authorize] // Temporarily disabled for Swagger testing
        public async Task<IActionResult> VerifySingleSide([FromForm] VerifySingleCccdRequest request)
        {
            if (request?.Image == null || request.Image.Length == 0)
            {
                return BadRequest(new ApiResponse<object>("No image file provided", null));
            }

            try
            {
                var result = await _ekycService.VerifyCccdAsync(request.Image);

                if (!result.IsValid)
                {
                    return Ok(new ApiResponse<CccdVerificationResultDto>(
                        result.ErrorMessage ?? "Verification failed",
                        result));
                }

                return Ok(new ApiResponse<CccdVerificationResultDto>(
                    "CCCD verified successfully",
                    result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Verify both front and back of CCCD
        /// </summary>
        /// <param name="request">Request containing front and back CCCD images</param>
        /// <returns>Combined verification result</returns>
        [HttpPost("verify-both")]
        [Consumes("multipart/form-data")]
        // [Authorize] // Temporarily disabled for Swagger testing
        public async Task<IActionResult> VerifyBothSides([FromForm] VerifyBothSidesCccdRequest request)
        {
            if (request?.FrontImage == null || request.FrontImage.Length == 0)
            {
                return BadRequest(new ApiResponse<object>("Front image is required", null));
            }

            if (request?.BackImage == null || request.BackImage.Length == 0)
            {
                return BadRequest(new ApiResponse<object>("Back image is required", null));
            }

            try
            {
                var result = await _ekycService.VerifyCccdBothSidesAsync(request.FrontImage, request.BackImage);

                if (!result.IsValid)
                {
                    return Ok(new ApiResponse<CccdVerificationResultDto>(
                        result.ErrorMessage ?? "Verification failed",
                        result));
                }

                return Ok(new ApiResponse<CccdVerificationResultDto>(
                    "CCCD verified successfully",
                    result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error: {ex.Message}", null));
            }
        }

        /// <summary>
        /// Verify face matching between selfie and CCCD photo
        /// </summary>
        /// <param name="request">Request containing selfie and CCCD front image</param>
        /// <returns>Face matching result with similarity score</returns>
        [HttpPost("verify-face-match")]
        [Consumes("multipart/form-data")]
        // [Authorize] // Temporarily disabled for Swagger testing
        public async Task<IActionResult> VerifyFaceMatch([FromForm] VerifyFaceMatchRequest request)
        {
            if (request?.SelfieImage == null || request.SelfieImage.Length == 0)
            {
                return BadRequest(new ApiResponse<object>("Selfie image is required", null));
            }

            if (request?.CccdFrontImage == null || request.CccdFrontImage.Length == 0)
            {
                return BadRequest(new ApiResponse<object>("CCCD front image is required", null));
            }

            try
            {
                var result = await _faceMatchService.CompareFacesAsync(request.SelfieImage, request.CccdFrontImage);

                if (!result.IsMatched)
                {
                    return Ok(new ApiResponse<FaceMatchResultDto>(
                        result.ErrorMessage ?? $"Face not matched (Score: {result.MatchScore:P})",
                        result));
                }

                // Check if match score meets threshold (70%)
                if (result.MatchScore < 0.7)
                {
                    return Ok(new ApiResponse<FaceMatchResultDto>(
                        $"Face matching score too low ({result.MatchScore:P}). Required: ≥70%",
                        result));
                }

                return Ok(new ApiResponse<FaceMatchResultDto>(
                    $"Face matched successfully (Score: {result.MatchScore:P})",
                    result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>($"Error: {ex.Message}", null));
            }
        }
    }
}

