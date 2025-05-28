using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess
{
    public class ShareItDbContextFactory : IDesignTimeDbContextFactory<ShareItDbContext>
    {
        public ShareItDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionBuilder = new DbContextOptionsBuilder<ShareItDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            optionBuilder.UseSqlServer(connectionString);

            return new ShareItDbContext(optionBuilder.Options);
        }
    }
}
