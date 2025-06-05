using BusinessObject.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utilities
{
    public static class ValidationErrorHelper
    {
        public static IActionResult CreateFormattedValidationErrorResponse(ActionContext context)
        {
            var errorDetails = context.ModelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToList()
                );

            var apiResponse = new ApiResponse<Dictionary<string, List<string>>>(
                "Validation failed",
                errorDetails
            );

            return new BadRequestObjectResult(apiResponse);
        }
    }
}
