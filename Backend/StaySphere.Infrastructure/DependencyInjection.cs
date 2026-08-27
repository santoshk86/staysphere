using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StaySphere.Application.Common;
using StaySphere.Infrastructure.Booking;
using StaySphere.Infrastructure.Persistence;
using StaySphere.Infrastructure.Persistence.Seeding;
using StaySphere.Infrastructure.Time;

namespace StaySphere.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("StaySphere")
            ?? "Data Source=staysphere.db";

        services.AddDbContext<StaySphereDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IStaySphereDbContext>(sp => sp.GetRequiredService<StaySphereDbContext>());
        services.AddScoped<JsonRoomCatalogSeeder>();
        services.AddScoped<DatabaseInitializer>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IBookingReferenceGenerator, BookingReferenceGenerator>();

        return services;
    }
}
