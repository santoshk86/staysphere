using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Seeding;

/// <summary>
/// Startup seeder that adds rooms (and optionally their room types / amenities)
/// from one or more JSON files. Idempotent: a record is inserted only when no row
/// with that explicit <c>Id</c> already exists, so restarting the API never
/// duplicates or overwrites data.
///
/// This runs after <c>Database.Migrate()</c> — it is deliberately not part of an
/// EF migration, because migrations must be deterministic and must not depend on
/// external files.
/// </summary>
public sealed class JsonRoomCatalogSeeder
{
    private const string DefaultRelativePath = "Data/room-seed.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly StaySphereDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JsonRoomCatalogSeeder> _logger;

    public JsonRoomCatalogSeeder(
        StaySphereDbContext db,
        IConfiguration configuration,
        ILogger<JsonRoomCatalogSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var path in ResolvePaths())
        {
            await SeedFileAsync(path, cancellationToken);
        }
    }

    private IEnumerable<string> ResolvePaths()
    {
        var configured = _configuration.GetSection("Seeding:RoomsFiles")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        if (configured.Length == 0)
        {
            configured = new[] { DefaultRelativePath };
        }

        foreach (var entry in configured)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            yield return Path.IsPathRooted(entry)
                ? entry
                : Path.Combine(AppContext.BaseDirectory, entry);
        }
    }

    private async Task SeedFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            _logger.LogInformation("Room seed file not found ({Path}); skipping.", path);
            return;
        }

        RoomCatalogSeedFile? file;
        try
        {
            await using var stream = File.OpenRead(path);
            file = await JsonSerializer.DeserializeAsync<RoomCatalogSeedFile>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogError(ex, "Could not read room seed file {Path}; skipping it.", path);
            return;
        }

        if (file is null)
        {
            _logger.LogWarning("Room seed file {Path} was empty; skipping.", path);
            return;
        }

        var added = 0;
        added += await SeedAmenitiesAsync(file.Amenities, cancellationToken);
        added += await SeedRoomTypesAsync(file.RoomTypes, cancellationToken);
        added += await SeedRoomsAsync(file.Rooms, cancellationToken);

        _logger.LogInformation(
            added > 0
                ? "Room seed file {Path}: added {Count} new record(s)."
                : "Room seed file {Path}: already up to date, nothing added.",
            path, added);
    }

    private async Task<int> SeedAmenitiesAsync(List<AmenitySeed>? amenities, CancellationToken cancellationToken)
    {
        if (amenities is null || amenities.Count == 0)
        {
            return 0;
        }

        var existingIds = (await _db.Amenities.Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingNames = (await _db.Amenities.Select(a => a.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in amenities)
        {
            if (existingIds.Contains(seed.Id))
            {
                continue;
            }

            if (existingNames.Contains(seed.Name))
            {
                _logger.LogWarning("Amenity '{Name}' already exists with a different id; skipping seed id {Id}.", seed.Name, seed.Id);
                continue;
            }

            try
            {
                var amenity = new Amenity(seed.Name);
                _db.Entry(amenity).Property(a => a.Id).CurrentValue = seed.Id;
                _db.Amenities.Add(amenity);
                existingIds.Add(seed.Id);
                existingNames.Add(seed.Name);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid amenity seed (id {Id}).", seed.Id);
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }

    private async Task<int> SeedRoomTypesAsync(List<RoomTypeSeed>? roomTypes, CancellationToken cancellationToken)
    {
        if (roomTypes is null || roomTypes.Count == 0)
        {
            return 0;
        }

        var existingIds = (await _db.RoomTypes.Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingNames = (await _db.RoomTypes.Select(t => t.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in roomTypes)
        {
            if (existingIds.Contains(seed.Id))
            {
                continue;
            }

            if (existingNames.Contains(seed.Name))
            {
                _logger.LogWarning("Room type '{Name}' already exists with a different id; skipping seed id {Id}.", seed.Name, seed.Id);
                continue;
            }

            try
            {
                var roomType = new RoomType(seed.Name, seed.Description, seed.PricePerNight, seed.MaxGuests, seed.ImageUrl ?? string.Empty);
                _db.Entry(roomType).Property(t => t.Id).CurrentValue = seed.Id;
                _db.RoomTypes.Add(roomType);

                foreach (var amenityId in (seed.AmenityIds ?? new List<int>()).Distinct())
                {
                    var amenity = await _db.Amenities.FindAsync(new object?[] { amenityId }, cancellationToken);
                    if (amenity is null)
                    {
                        _logger.LogWarning("Room type '{Name}' references unknown amenity id {AmenityId}; link skipped.", seed.Name, amenityId);
                        continue;
                    }

                    _db.RoomTypeAmenities.Add(new RoomTypeAmenity(roomType, amenity));
                }

                existingIds.Add(seed.Id);
                existingNames.Add(seed.Name);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid room type seed (id {Id}).", seed.Id);
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }

    private async Task<int> SeedRoomsAsync(List<RoomSeed>? rooms, CancellationToken cancellationToken)
    {
        if (rooms is null || rooms.Count == 0)
        {
            return 0;
        }

        var existingRoomIds = (await _db.Rooms.Select(r => r.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingRoomNumbers = (await _db.Rooms.Select(r => r.RoomNumber).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in rooms)
        {
            // The requested rule: match on the explicit room id. If a room with
            // that id already exists, leave it untouched.
            if (existingRoomIds.Contains(seed.Id))
            {
                continue;
            }

            if (existingRoomNumbers.Contains(seed.RoomNumber))
            {
                _logger.LogWarning("Room number {RoomNumber} already exists; skipping seed id {Id}.", seed.RoomNumber, seed.Id);
                continue;
            }

            var roomType = await _db.RoomTypes.FindAsync(new object?[] { seed.RoomTypeId }, cancellationToken);
            if (roomType is null)
            {
                _logger.LogWarning("Room {RoomNumber} references unknown room type {RoomTypeId}; skipping.", seed.RoomNumber, seed.RoomTypeId);
                continue;
            }

            try
            {
                var room = new Room(seed.RoomNumber, roomType);
                _db.Entry(room).Property(r => r.Id).CurrentValue = seed.Id;
                _db.Rooms.Add(room);
                existingRoomIds.Add(seed.Id);
                existingRoomNumbers.Add(seed.RoomNumber);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping invalid room seed (id {Id}).", seed.Id);
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return added;
    }
}
