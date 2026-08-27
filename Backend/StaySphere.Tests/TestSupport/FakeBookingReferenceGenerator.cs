using StaySphere.Application.Common;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="IBookingReferenceGenerator"/>. Emits any scripted
/// values first (used to force a collision), then falls back to sequential unique
/// references. Records how many times it was asked for a value.
/// </summary>
public sealed class FakeBookingReferenceGenerator : IBookingReferenceGenerator
{
    private readonly Queue<string> _scripted;
    private int _counter;

    public FakeBookingReferenceGenerator(params string[] scripted) => _scripted = new Queue<string>(scripted);

    public int GenerateCallCount { get; private set; }

    public string Generate()
    {
        GenerateCallCount++;
        return _scripted.Count > 0 ? _scripted.Dequeue() : $"STAY-TEST{++_counter:D4}";
    }
}
