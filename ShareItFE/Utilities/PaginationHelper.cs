using System.Collections.Generic;

namespace ShareItFE.Utilities
{
    public static class PaginationHelper
    {
        /// <summary>
        /// Generates a smart pagination array with ellipsis
        /// Example for page 5 of 20: [1, "...", 4, 5, 6, "...", 20]
        /// </summary>
        /// <param name="currentPage">Current page number</param>
        /// <param name="totalPages">Total number of pages</param>
        /// <param name="maxVisible">Maximum number of visible page numbers (default 7)</param>
        /// <returns>List of page numbers or "..." for ellipsis</returns>
        public static List<object> GetPaginationItems(int currentPage, int totalPages, int maxVisible = 7)
        {
            var items = new List<object>();

            if (totalPages <= maxVisible)
            {
                // Show all pages if total is small
                for (int i = 1; i <= totalPages; i++)
                {
                    items.Add(i);
                }
            }
            else
            {
                // Always show first page
                items.Add(1);

                int start, end;

                if (currentPage <= 3)
                {
                    // Near beginning: [1] [2] [3] [4] [5] [...] [Last]
                    start = 2;
                    end = 5;
                    
                    for (int i = start; i <= end && i < totalPages; i++)
                    {
                        items.Add(i);
                    }
                    
                    if (end < totalPages - 1)
                    {
                        items.Add("...");
                    }
                }
                else if (currentPage >= totalPages - 2)
                {
                    // Near end: [1] [...] [Last-4] [Last-3] [Last-2] [Last-1] [Last]
                    items.Add("...");
                    
                    start = totalPages - 4;
                    end = totalPages - 1;
                    
                    for (int i = start; i <= end && i > 1; i++)
                    {
                        items.Add(i);
                    }
                }
                else
                {
                    // Middle: [1] [...] [Current-1] [Current] [Current+1] [...] [Last]
                    items.Add("...");
                    
                    for (int i = currentPage - 1; i <= currentPage + 1; i++)
                    {
                        if (i > 1 && i < totalPages)
                        {
                            items.Add(i);
                        }
                    }
                    
                    items.Add("...");
                }

                // Always show last page
                if (totalPages > 1)
                {
                    items.Add(totalPages);
                }
            }

            return items;
        }
    }
}

