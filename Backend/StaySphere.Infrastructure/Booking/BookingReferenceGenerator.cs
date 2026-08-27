using System.Security.Cryptography;
using StaySphere.Application.Common;

namespace StaySphere.Infrastructure.Booking;

/// <summary>
/// Generates references of the form <c>STAY-XXXXXXXX</c> using Crockford base32
/// (no I, L, O, U) so they are easy to read aloud and hard to guess. The public
/// reference is deliberately not the database identifier.
/// </summary>
public sealed class BookingReferenceGenerator : IBookingReferenceGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int BodyLength = 8;

    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[BodyLength];
        RandomNumberGenerator.Fill(bytes);

        Span<char> body = stackalloc char[BodyLength];
        for (var i = 0; i < BodyLength; i++)
        {
            body[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return string.Concat("STAY-", new string(body));
    }
}
