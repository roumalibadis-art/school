namespace USTHBStudy.Infrastructure.Academic;

using Microsoft.Extensions.Caching.Memory;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>Shared dependencies for the academic services — bundled so concrete ctors stay one-liners.</summary>
public sealed class AcademicServiceDependencies
{
    public AcademicServiceDependencies(
        AppDbContext db,
        IDateTimeProvider clock,
        IMemoryCache cache,
        AcademicCacheSignal cacheSignal)
    {
        Db = db;
        Clock = clock;
        Cache = cache;
        CacheSignal = cacheSignal;
    }

    public AppDbContext Db { get; }

    public IDateTimeProvider Clock { get; }

    public IMemoryCache Cache { get; }

    public AcademicCacheSignal CacheSignal { get; }
}
