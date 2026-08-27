namespace StaySphere.Application.Common;

/// <summary>Produces human-friendly, unpredictable public booking references.</summary>
public interface IBookingReferenceGenerator
{
    string Generate();
}
