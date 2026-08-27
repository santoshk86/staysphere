using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StaySphere.Application.Common;
using StaySphere.Infrastructure.Persistence;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Boots the real API pipeline (routing, model binding, filters, the exception
/// middleware, JSON serialization, the application services and EF Core) against a
/// private in-memory SQLite database and a fixed clock.
///
/// The database is created by the app's own startup path (migrations + catalog
/// seed). <see cref="ResetReservationsAsync"/> clears reservations between tests so
/// each test starts from the catalog with no bookings.
/// </summary>
public sealed class StaySphereApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    /// <summary>Fixed "now" the API sees. Choose stay dates relative to this.</summary>
    public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    public DateOnly Today => Clock.Today;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Point the JSON room-catalog seeder at a file that does not exist so the
        // database contains only the deterministic catalog baked into the EF model
        // (see SeededCatalog). Without this the API would also load the extra rooms
        // from StaySphere.Api/Data/room-seed.json.
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeding:RoomsFiles:0"] = "test-no-catalog-seed-file.json",
            });
        });

        builder.ConfigureServices(services =>
        {
            Remove<DbContextOptions<StaySphereDbContext>>(services);
            Remove<DbContextOptions>(services);
            Remove<StaySphereDbContext>(services);
            Remove<IStaySphereDbContext>(services);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<StaySphereDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IStaySphereDbContext>(sp => sp.GetRequiredService<StaySphereDbContext>());

            Remove<IClock>(services);
            services.AddSingleton<IClock>(Clock);
        });
    }

    public async Task ResetReservationsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaySphereDbContext>();
        db.Reservations.RemoveRange(db.Reservations);
        await db.SaveChangesAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<StaySphereDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaySphereDbContext>();
        return await action(db);
    }

    private static void Remove<TService>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
