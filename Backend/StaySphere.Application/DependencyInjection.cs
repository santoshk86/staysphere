using Microsoft.Extensions.DependencyInjection;
using StaySphere.Application.Reservations;
using StaySphere.Application.Rooms;

namespace StaySphere.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IReservationService, ReservationService>();
        return services;
    }
}
