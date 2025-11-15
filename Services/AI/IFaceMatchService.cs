using BusinessObject.DTOs.AIDtos;
using Microsoft.AspNetCore.Http;

namespace Services.AI
{
    /// <summary>
    /// Interface for FPT.AI Face Matching Service
    /// Compares if two faces belong to the same person
    /// </summary>
    public interface IFaceMatchService
    {
        /// <summary>
        /// Compare selfie with CCCD photo to verify identity
        /// </summary>
        /// <param name="selfieImage">Selfie photo from user</param>
        /// <param name="cccdImage">Photo from CCCD</param>
        /// <returns>Match result with similarity score</returns>
        Task<FaceMatchResultDto> CompareFacesAsync(IFormFile selfieImage, IFormFile cccdImage);
    }
}

