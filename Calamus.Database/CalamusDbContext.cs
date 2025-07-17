using Microsoft.EntityFrameworkCore;

namespace Calamus.Database;

public class CalamusDbContext(DbContextOptions<CalamusDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
}

public static class CalamusInitializer
{
    public static async Task Initialize(this CalamusDbContext context)
    {
        await context.Database.MigrateAsync();
    }
}