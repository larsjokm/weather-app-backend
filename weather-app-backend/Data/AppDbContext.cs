using Microsoft.EntityFrameworkCore;
using weather_app_backend.Models;

namespace weather_app_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<IpLogDto> IpLogs { get; set; }
    public DbSet<ForecastLogDto> ForecastLogs { get; set; }
}