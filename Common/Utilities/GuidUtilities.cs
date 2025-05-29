using BusinessObject.DTOs.GuidValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Utilities
{
    public static class GuidUtilities
    {
        public static GuidValidationResult ValidateGuid(string id, Guid entityId)
        {
            if (!Guid.TryParse(id, out Guid guidId))
            {
                return new GuidValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid GUID format"
                };
            }

            if (guidId != entityId)
            {
                return new GuidValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "ID in route does not match entity ID"
                };
            }

            return new GuidValidationResult
            {
                IsValid = true,
                ParsedGuid = guidId
            };
        }
    }
}
