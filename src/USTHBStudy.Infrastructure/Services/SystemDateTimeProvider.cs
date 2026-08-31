namespace USTHBStudy.Infrastructure.Services;

using USTHBStudy.Application.Abstractions;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
