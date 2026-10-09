namespace USTHBStudy.Infrastructure.Auth.Google;

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.Application.Authorization;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence;

/// <summary>
/// Maps a validated Google identity onto a local account without ever weakening it:
/// <list type="bullet">
/// <item>Known (provider, subject) pair → that account.</item>
/// <item>Unknown, no local account with the email → a new <c>Student</c> account (never any other role).</item>
/// <item>Unknown, local account exists → link automatically only if that account's email is confirmed AND it holds no
/// privileged role. Otherwise the user must sign in with their existing method and link from their profile — this
/// blocks "pre-hijacking" (an attacker registering a victim's email with a password first).</item>
/// </list>
/// </summary>
public sealed class ExternalAccountService : IExternalAccountService
{
    public const string GoogleProvider = "Google";

    private static readonly string[] PrivilegedRoles = { Roles.Admin, Roles.Moderator };

    private readonly UserManager<ApplicationUser> _users;

    public ExternalAccountService(UserManager<ApplicationUser> users) => _users = users;

    public async Task<ExternalSignInResult> SignInWithGoogleAsync(GoogleIdentity identity, CancellationToken ct = default)
    {
        if (!identity.EmailVerified)
        {
            return new ExternalSignInResult(ExternalSignInStatus.EmailNotVerified, null);
        }

        var linked = await _users.FindByLoginAsync(GoogleProvider, identity.Subject);
        if (linked is not null)
        {
            return linked.IsActive
                ? new ExternalSignInResult(ExternalSignInStatus.SignedIn, linked.Id)
                : new ExternalSignInResult(ExternalSignInStatus.Blocked, linked.Id);
        }

        var existing = await _users.FindByEmailAsync(identity.Email);
        if (existing is null)
        {
            return await CreateStudentAsync(identity);
        }

        if (!existing.IsActive)
        {
            return new ExternalSignInResult(ExternalSignInStatus.Blocked, existing.Id);
        }

        var privileged = (await _users.GetRolesAsync(existing)).Any(r => PrivilegedRoles.Contains(r, StringComparer.Ordinal));
        if (privileged || !existing.EmailConfirmed)
        {
            return new ExternalSignInResult(ExternalSignInStatus.LinkRequired, existing.Id);
        }

        var added = await _users.AddLoginAsync(existing, new UserLoginInfo(GoogleProvider, identity.Subject, GoogleProvider));
        return added.Succeeded
            ? new ExternalSignInResult(ExternalSignInStatus.SignedIn, existing.Id)
            : new ExternalSignInResult(ExternalSignInStatus.LinkRequired, existing.Id);
    }

    private async Task<ExternalSignInResult> CreateStudentAsync(GoogleIdentity identity)
    {
        var fallbackName = identity.Email.Split('@')[0];
        var user = new ApplicationUser
        {
            UserName = identity.Email,
            Email = identity.Email,
            FirstName = Clip(identity.GivenName ?? fallbackName),
            LastName = Clip(identity.FamilyName ?? string.Empty),
            EmailConfirmed = true, // Google verified it.
            IsActive = true,
        };

        // No password at all: password sign-in is impossible until the user sets one through a reset flow.
        var created = await _users.CreateAsync(user);
        if (!created.Succeeded)
        {
            // Lost a race with a concurrent first sign-in — resolve it to the winner.
            var winner = await _users.FindByEmailAsync(identity.Email);
            if (winner is not null && await _users.FindByLoginAsync(GoogleProvider, identity.Subject) is { } mine && mine.Id == winner.Id)
            {
                return new ExternalSignInResult(ExternalSignInStatus.SignedIn, winner.Id);
            }

            return new ExternalSignInResult(ExternalSignInStatus.LinkRequired, winner?.Id);
        }

        await _users.AddToRoleAsync(user, Roles.Student);
        await _users.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, identity.Subject, GoogleProvider));
        return new ExternalSignInResult(ExternalSignInStatus.Created, user.Id);
    }

    public async Task<LinkStatus> LinkGoogleAsync(Guid userId, GoogleIdentity identity, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return LinkStatus.UserNotFound;
        }

        if (!user.IsActive)
        {
            return LinkStatus.Blocked;
        }

        var owner = await _users.FindByLoginAsync(GoogleProvider, identity.Subject);
        if (owner is not null)
        {
            return owner.Id == userId ? LinkStatus.AlreadyLinked : LinkStatus.LinkedToAnotherAccount;
        }

        var result = await _users.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, identity.Subject, GoogleProvider));
        return result.Succeeded ? LinkStatus.Linked : LinkStatus.LinkedToAnotherAccount;
    }

    public async Task<bool> IsGoogleLinkedAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        return user is not null && (await _users.GetLoginsAsync(user)).Any(l => l.LoginProvider == GoogleProvider);
    }

    private static string Clip(string value) => value.Length <= 100 ? value : value[..100];
}

public sealed class ExternalLoginTicketService : IExternalLoginTicketService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ExternalLoginTicketService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> IssueAsync(Guid userId, ExternalTicketPurpose purpose, TimeSpan lifetime, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var now = _clock.UtcNow;
        _db.ExternalLoginTickets.Add(new ExternalLoginTicket
        {
            TokenHash = Hash(token),
            UserId = userId,
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<Guid?> RedeemAsync(string ticket, ExternalTicketPurpose purpose, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 200)
        {
            return null;
        }

        var hash = Hash(ticket);
        var now = _clock.UtcNow;

        // Single statement: only the first redemption of an unexpired ticket can flip UsedAt.
        var consumed = await _db.ExternalLoginTickets
            .Where(t => t.TokenHash == hash && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.UsedAt, now), ct);
        if (consumed != 1)
        {
            return null;
        }

        return await _db.ExternalLoginTickets.AsNoTracking().Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UserId).FirstOrDefaultAsync(ct);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
