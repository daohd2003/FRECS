using Microsoft.AspNetCore.Http;

namespace BusinessObject.DTOs.AIDtos
{
    /// <summary>
    /// Request DTO for single CCCD image verification
    /// </summary>
    public class VerifySingleCccdRequest
    {
        /// <summary>
        /// CCCD image file (front or back)
        /// </summary>
        public IFormFile Image { get; set; } = null!;
    }

    /// <summary>
    /// Request DTO for both sides CCCD verification
    /// </summary>
    public class VerifyBothSidesCccdRequest
    {
        /// <summary>
        /// Front side of CCCD
        /// </summary>
        public IFormFile FrontImage { get; set; } = null!;

        /// <summary>
        /// Back side of CCCD
        /// </summary>
        public IFormFile BackImage { get; set; } = null!;
    }

    /// <summary>
    /// Request DTO for Face Matching verification
    /// </summary>
    public class VerifyFaceMatchRequest
    {
        /// <summary>
        /// Selfie image of the person
        /// </summary>
        public IFormFile SelfieImage { get; set; } = null!;

        /// <summary>
        /// Front side of CCCD (contains photo)
        /// </summary>
        public IFormFile CccdFrontImage { get; set; } = null!;
    }
}

