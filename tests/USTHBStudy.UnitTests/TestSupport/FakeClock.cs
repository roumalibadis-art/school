namespace USTHBStudy.UnitTests.TestSupport;

using USTHBStudy.Application.Abstractions;

public sealed class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
