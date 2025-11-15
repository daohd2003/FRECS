using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;

namespace Services.AI
{
    /// <summary>
    /// Interface for FPT.AI eKYC Service - ID Card Recognition
    /// </summary>
    public interface IEkycService
    {
        /// <summary>
        /// Verify CCCD (Vietnamese ID Card) using FPT.AI Vision API
        /// </summary>
        /// <param name="idCardImage">Front or back image of the ID card</param>
        /// <returns>Verification result with extracted data</returns>
        Task<CccdVerificationResultDto> VerifyCccdAsync(IFormFile idCardImage);

        /// <summary>
        /// Verify both front and back of CCCD
        /// </summary>
        /// <param name="frontImage">Front side of ID card</param>
        /// <param name="backImage">Back side of ID card</param>
        /// <returns>Combined verification result</returns>
        Task<CccdVerificationResultDto> VerifyCccdBothSidesAsync(IFormFile frontImage, IFormFile backImage);
    }
}

