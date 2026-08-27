using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StaySphere.Infrastructure.Persistence;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// A real <see cref="StaySphereDbContext"/> backed by a private in-memory SQLite
/// database. The connection is held open for the lifetime of the instance so the
/// database survives between contexts.
///
/// Why real SQLite rather than a fake <c>IStaySphereDbContext</c>: the behaviour
/// under test is our LINQ — the half-open overlap predicate, the capacity filter,
/// ordering, owned-type mapping of <c>DateRange</c>. Those only mean something when
/// they are actually translated to SQL and executed.
///
/// The schema is created with <see cref="DatabaseFacade.EnsureCreated"/>, which
/// also applies the catalog seed data from the model (4 room types, 8 rooms,
/// 10 amenities). No reservations are seeded — each test adds exactly what it needs.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<StaySphereDbContext> _options;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<StaySphereDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A fresh context (fresh EF identity map) over the same database.</summary>
    public StaySphereDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
