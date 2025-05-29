using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.GuidValidation
{
    public class GuidValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Guid ParsedGuid { get; set; }
    }
}
