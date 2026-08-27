namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Well-known ids and values from the catalog seed baked into the EF model
/// (see <c>InitialCreate</c> migration / <c>RoomTypeConfiguration</c> /
/// <c>RoomConfiguration</c>). Tests use these instead of re-seeding a catalog by
/// hand so they exercise the same reference data the product ships with.
/// </summary>
public static class SeededCatalog
{
    // RoomType 1 — "Standard Queen", 2 guests, $99.00
    public const int StandardQueenTypeId = 1;
    public const decimal StandardQueenPrice = 99.00m;
    public const int StandardQueenCapacity = 2;
    public const int StandardQueenRoom101Id = 1;
    public const int StandardQueenRoom102Id = 2;
    public const int StandardQueenRoom103Id = 3;

    // RoomType 2 — "Deluxe King", 2 guests, $159.00
    public const int DeluxeKingRoom201Id = 4;
    public const int DeluxeKingRoom202Id = 5;

    // RoomType 3 — "Family Suite", 4 guests, $249.00
    public const int FamilySuiteTypeId = 3;
    public const decimal FamilySuitePrice = 249.00m;
    public const int FamilySuiteCapacity = 4;
    public const int FamilySuiteRoom301Id = 6;
    public const int FamilySuiteRoom302Id = 7;

    // RoomType 4 — "Executive Suite", 3 guests, $399.00
    public const int ExecutiveSuiteCapacity = 3;
    public const int ExecutiveSuiteRoom401Id = 8;

    public const int RoomCount = 8;

    /// <summary>Room numbers in the order search returns them (price asc, then room number).</summary>
    public static readonly string[] RoomNumbersByPriceThenNumber =
        ["101", "102", "103", "201", "202", "301", "302", "401"];
}
