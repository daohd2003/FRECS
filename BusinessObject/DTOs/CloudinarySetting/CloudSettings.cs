using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.CloudinarySetting
{
    public class CloudSettings
    {
        public string CloudName { get; set; } = String.Empty;
        public string APIKey { get; set; } = String.Empty;
        public string APISecret { get; set; } = String.Empty;
    }
}
