using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ShoppingCart.Common.Patterns.Factory;

public class DatabaseFactory : IDatabaseFactory
{
    private readonly IConfiguration _configuration;

    public DatabaseFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbContext CreateDbContext(string connectionString, string provider = "SqlServer")
    {
        return provider.ToLower() switch
        {
            "sqlserver" => CreateSqlServerContext(connectionString),
            "postgresql" => CreatePostgreSqlContext(connectionString),
            _ => throw new ArgumentException($"Unsupported database provider: {provider}")
        };
    }

    public async Task<DbContext> CreateDbContextAsync(string connectionString, string provider = "SqlServer")
    {
        return await Task.FromResult(CreateDbContext(connectionString, provider));
    }

    private DbContext CreateSqlServerContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private DbContext CreatePostgreSqlContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

// Placeholder ApplicationDbContext - this would be implemented in each microservice
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configure entities here
    }
} 