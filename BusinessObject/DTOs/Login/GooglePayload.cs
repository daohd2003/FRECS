using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.Login
{
    public class GooglePayload
    {
        public string Sub { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }
}
