using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace ShareItFE.Pages.AI
{
    public class AIGeneration : PageModel
    {
        private readonly ILogger<AIGeneration> _logger;

        public AIGeneration(ILogger<AIGeneration> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}