namespace USTHBStudy.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock so time-dependent logic (Premium expiry — PRD §24,
/// token lifetimes) is deterministic in tests. No business code calls <c>DateTime.UtcNow</c> directly.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
