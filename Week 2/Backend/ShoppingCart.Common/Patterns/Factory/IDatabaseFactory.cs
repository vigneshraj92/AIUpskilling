using Microsoft.EntityFrameworkCore;

namespace ShoppingCart.Common.Patterns.Factory;

public interface IDatabaseFactory
{
    DbContext CreateDbContext(string connectionString, string provider = "SqlServer");
    Task<DbContext> CreateDbContextAsync(string connectionString, string provider = "SqlServer");
} 