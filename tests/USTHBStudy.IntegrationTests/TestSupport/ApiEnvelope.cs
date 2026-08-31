namespace USTHBStudy.IntegrationTests.TestSupport;

using System.Net.Http.Json;
using System.Text.Json;

/// <summary>Mirror of the API response envelope (PRD §47) for deserialization in tests.</summary>
public sealed class ApiEnvelope<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public T? Data { get; set; }
}

public sealed record AuthResultDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool IsPremium,
    DateTime? PremiumExpiresAt,
    List<string> Roles,
    List<string> Permissions);

public static class HttpResponseExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(this HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(Options);
        return envelope ?? throw new InvalidOperationException("Response body was empty.");
    }
}
