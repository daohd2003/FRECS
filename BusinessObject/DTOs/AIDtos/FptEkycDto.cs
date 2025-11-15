using System.Text.Json.Serialization;

namespace BusinessObject.DTOs.AIDtos
{
    /// <summary>
    /// DTO for FPT.AI eKYC ID Card Recognition Response
    /// </summary>
    public class FptEkycResponseDto
    {
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("data")]
        public List<FptEkycDataDto>? Data { get; set; }
    }

    public class FptEkycDataDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("id_prob")]
        public string? IdProb { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_prob")]
        public string? NameProb { get; set; }

        [JsonPropertyName("dob")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("dob_prob")]
        public string? DateOfBirthProb { get; set; }

        [JsonPropertyName("sex")]
        public string? Sex { get; set; }

        [JsonPropertyName("sex_prob")]
        public string? SexProb { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("nationality_prob")]
        public string? NationalityProb { get; set; }

        [JsonPropertyName("home")]
        public string? Address { get; set; }

        [JsonPropertyName("home_prob")]
        public string? AddressProb { get; set; }

        [JsonPropertyName("address")]
        public string? ResidenceAddress { get; set; }

        [JsonPropertyName("address_prob")]
        public string? ResidenceAddressProb { get; set; }

        [JsonPropertyName("doe")]
        public string? DateOfExpiry { get; set; }

        [JsonPropertyName("doe_prob")]
        public string? DateOfExpiryProb { get; set; }

        [JsonPropertyName("type")]
        public string? CardType { get; set; }

        [JsonPropertyName("type_new")]
        public string? CardTypeNew { get; set; }

        [JsonPropertyName("ethnicity")]
        public string? Ethnicity { get; set; }

        [JsonPropertyName("ethnicity_prob")]
        public string? EthnicityProb { get; set; }

        [JsonPropertyName("religion")]
        public string? Religion { get; set; }

        [JsonPropertyName("religion_prob")]
        public string? ReligionProb { get; set; }

        [JsonPropertyName("issue_date")]
        public string? IssueDate { get; set; }

        [JsonPropertyName("issue_date_prob")]
        public string? IssueDateProb { get; set; }

        [JsonPropertyName("issue_loc")]
        public string? IssueLocation { get; set; }

        [JsonPropertyName("issue_loc_prob")]
        public string? IssueLocationProb { get; set; }

        [JsonPropertyName("features")]
        public string? Features { get; set; }

        [JsonPropertyName("features_prob")]
        public string? FeaturesProb { get; set; }

        [JsonPropertyName("recent_location")]
        public string? RecentLocation { get; set; }

        [JsonPropertyName("recent_location_prob")]
        public string? RecentLocationProb { get; set; }

        [JsonPropertyName("overall_score")]
        public string? OverallScore { get; set; }
    }

    /// <summary>
    /// Simplified DTO for frontend display
    /// </summary>
    public class CccdVerificationResultDto
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? IdNumber { get; set; }
        public string? FullName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? Nationality { get; set; }
        public string? Address { get; set; }
        public string? DateOfExpiry { get; set; }
        public string? IssueDate { get; set; }
        public string? CardType { get; set; }
        public double Confidence { get; set; } // Average confidence score
    }

    /// <summary>
    /// DTO for FPT.AI Liveness Detection Response
    /// </summary>
    public class FptLivenessResponseDto
    {
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("data")]
        public FptLivenessDataDto? Data { get; set; }
    }

    public class FptLivenessDataDto
    {
        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("is_real")]
        public bool IsReal { get; set; }

        [JsonPropertyName("msg")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Simplified Liveness result for frontend
    /// </summary>
    public class LivenessResultDto
    {
        public bool IsReal { get; set; }
        public double Confidence { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// DTO for FPT.AI FaceMatch Response
    /// </summary>
    public class FptFaceMatchResponseDto
    {
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("data")]
        public FptFaceMatchDataDto? Data { get; set; }
    }

    public class FptFaceMatchDataDto
    {
        [JsonPropertyName("matching")]
        public double Matching { get; set; }

        [JsonPropertyName("msg")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Simplified FaceMatch result for frontend
    /// </summary>
    public class FaceMatchResultDto
    {
        public bool IsMatched { get; set; }
        public double MatchScore { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

