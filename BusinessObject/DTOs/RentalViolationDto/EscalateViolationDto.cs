namespace BusinessObject.DTOs.RentalViolationDto
{
    /// <summary>
    /// DTO for escalating a violation to admin
    /// </summary>
    public class EscalateViolationDto
    {
        /// <summary>
        /// Optional reason for escalation
        /// </summary>
        public string? Reason { get; set; }
    }
}
