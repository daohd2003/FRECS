using System.Security.Claims;
using BusinessObject.DTOs.ReportDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Services.ReportService;

namespace ShareItAPI.Controllers.OData
{
    [ApiController]
    [Route("odata")] // Gốc OData
    public class ReportsController : ODataController
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // GET odata/unassigned
        [EnableQuery]
        [HttpGet("unassigned")]
        [Authorize(Roles = "admin,staff")]
        public IQueryable<ReportViewModel> GetUnassigned()
        {
            return _reportService.GetUnassignedReports();
        }

        // GET odata/mytasks
        [EnableQuery]
        [HttpGet("mytasks")]
        [Authorize(Roles = "admin,staff")]
        public IQueryable<ReportViewModel> GetMyTasks()
        {
            var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            return _reportService.GetReportsByAdminId(adminId);
        }
    }
}