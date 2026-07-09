using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace uptime_oco;

public class UptimeOcoContextFactory : IDesignTimeDbContextFactory<UptimeOcoContext>
{
    public UptimeOcoContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UptimeOcoContext>()
            .UseSqlite("Data Source=uptime-oco.db")
            .Options;

        return new UptimeOcoContext(options);
    }
}
