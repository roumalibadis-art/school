namespace USTHBStudy.Infrastructure.Academic;

/// <summary>
/// A monotonically-increasing version stamp for cached academic reference data (PRD §56).
/// Any academic write bumps it; cache keys embed the current version, so stale entries are
/// simply never read again (and expire on their own TTL). Registered as a singleton.
/// </summary>
public sealed class AcademicCacheSignal
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Bump() => Interlocked.Increment(ref _version);
}
