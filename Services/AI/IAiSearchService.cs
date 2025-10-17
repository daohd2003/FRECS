using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AI
{
    public interface IAiSearchService
    {
        Task<string> AskAboutFRECSAsync(string question, Guid? userId = null);
    }
}
