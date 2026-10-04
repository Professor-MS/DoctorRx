using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DoctorRx.Infrastructure.Data;

public class DoctorRxDbContextFactory : IDesignTimeDbContextFactory<DoctorRxDbContext>
{
    public DoctorRxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DoctorRxDbContext>();
        optionsBuilder.UseSqlite("Data Source=doctorrx_design.db");

        return new DoctorRxDbContext(optionsBuilder.Options);
    }
}
