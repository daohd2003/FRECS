using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.BankAccounts
{
    public class BankAccountCreateDto
    {
        [Required(ErrorMessage = "Bank name is required.")]
        [StringLength(255, ErrorMessage = "Bank name cannot exceed 255 characters.")]
        [NotOnlyWhitespace(ErrorMessage = "Bank name cannot contain only whitespace.")]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account number is required.")]
        [StringLength(17, MinimumLength = 8, ErrorMessage = "Account number must be between 8 and 17 digits.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter a valid account number (digits only).")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account holder name is required.")]
        [StringLength(255, ErrorMessage = "Account holder name cannot exceed 255 characters.")]
        [NotOnlyWhitespace(ErrorMessage = "Account holder name cannot contain only whitespace.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name cannot contain special characters.")]
        public string AccountHolderName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Routing number cannot exceed 50 characters.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Please enter a valid routing number (digits only).")]
        public string? RoutingNumber { get; set; }

        public bool IsPrimary { get; set; }
    }

    // Custom validation attribute to check for whitespace-only strings
    public class NotOnlyWhitespaceAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage ?? "Field cannot contain only whitespace.");
        }
    }
}
