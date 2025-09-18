using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BusinessObject.Validators
{
    public class NumericAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string password)
            {
                if (!Regex.IsMatch(password, @"\d"))
                {
                    return new ValidationResult("Password must contain at least one number.");
                }
            }
            return ValidationResult.Success;
        }
    }
}
