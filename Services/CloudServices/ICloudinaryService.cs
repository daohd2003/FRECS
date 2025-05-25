using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Services.CloudServices
{
    public interface ICloudinaryService
    {
        Task<string> UploadImage(IFormFile file, int userId, string projectName, string folderType);
    }
}
