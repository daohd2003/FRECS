namespace BusinessObject.DTOs.PolicyConfigDto
{
    public class PolicyConfigDto
    {
        public Guid Id { get; set; }
        public string PolicyName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedByAdminId { get; set; }
        public string? UpdatedByAdminName { get; set; }
    }
}
