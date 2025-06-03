using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ApiResponses
{
    public class ApiResponse<T>
    {
        public string Message { get; set; }
        public T Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(string message, T data)
        {
            Message = message;
            Data = data;
        }
    }
}
