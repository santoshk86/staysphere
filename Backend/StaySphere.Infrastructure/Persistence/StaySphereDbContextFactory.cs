using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StaySphere.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> so migrations can be created without
/// starting the API host.
/// </summary>
public sealed class StaySphereDbContextFactory : IDesignTimeDbContextFactory<StaySphereDbContext>
{
    public StaySphereDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StaySphereDbContext>()
            .UseSqlite("Data Source=staysphere.db")
            .Options;

        return new StaySphereDbContext(options);
    }
}
