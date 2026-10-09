namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>Google sign-in: success, failure modes, account linking and privilege safety (spec test 15).</summary>
public class GoogleAuthTests
{
    private const string Frontend = "http://localhost:3000";

    private static JsonElement Data(string json) => ClassificationHarness.Data(json);

    private static GoogleIdentity Identity(string sub, string email, bool verified = true) =>
        new(sub, email, verified, "Gina", "Googler");

    private sealed record Setup(ClassificationHarness Harness, FakeGoogleOidcClient Google) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Harness.DisposeAsync();

        public WebApplicationFactory<Program> Factory => Harness.Factory;

        public HttpClient Browser() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        /// <summary>Runs start → callback in one "browser" and returns the final Location.</summary>
        public async Task<string> SignInAsync(string code, string? returnUrl = null, HttpClient? browser = null)
        {
            browser ??= Browser();
            var start = await browser.GetAsync("/api/auth/google/start" + (returnUrl is null ? "" : $"?returnUrl={Uri.EscapeDataString(returnUrl)}"));
            start.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var state = QueryOf(start.Headers.Location!)["state"];
            var callback = await browser.GetAsync($"/api/auth/google/callback?code={code}&state={Uri.EscapeDataString(state)}");
            callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return callback.Headers.Location!.ToString();
        }

        public async Task<HttpResponseMessage> ExchangeAsync(string ticket) =>
            await Factory.CreateClient().PostAsJsonAsync("/api/auth/google/exchange", new { ticket });
    }

    private static async Task<Setup> CreateAsync()
    {
        var google = new FakeGoogleOidcClient();
        var factory = new ApiFactory
        {
            ConfigureExtraServices = services =>
            {
                services.RemoveAll<IGoogleOidcClient>();
                services.AddSingleton<IGoogleOidcClient>(google);
            },
        };
        factory.Settings["Authentication:Google:FrontendBaseUrl"] = Frontend;
        return new Setup(await ClassificationHarness.CreateAsync(factory: factory), google);
    }

    private static Dictionary<string, string> QueryOf(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : string.Empty));

    private static string TicketOf(string location) => QueryOf(new Uri(location))["ticket"];

    // ------------------------------------------------------------------ configuration

    [Fact]
    public async Task Google_is_off_unless_configured_and_the_ui_can_tell()
    {
        await using var factoryOwner = await ClassificationHarness.CreateAsync(); // real client, no credentials
        var client = factoryOwner.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Data(await client.GetStringAsync("/api/auth/providers")).GetProperty("google").GetBoolean().Should().BeFalse();
        (await client.GetAsync("/api/auth/google/start")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Providers_endpoint_reports_google_when_configured()
    {
        await using var s = await CreateAsync();
        Data(await s.Factory.CreateClient().GetStringAsync("/api/auth/providers")).GetProperty("google").GetBoolean().Should().BeTrue();
    }

    // ------------------------------------------------------------------ successful sign-in

    [Fact]
    public async Task First_google_sign_in_creates_a_student_account_and_issues_normal_tokens()
    {
        await using var s = await CreateAsync();
        s.Google.Register("good", Identity("sub-1", "gina@example.test"));

        var location = await s.SignInAsync("good", "/documents/some-doc");
        location.Should().StartWith($"{Frontend}/auth/google/complete?ticket=");
        QueryOf(new Uri(location))["returnUrl"].Should().Be("/documents/some-doc");

        var exchange = await s.ExchangeAsync(TicketOf(location));
        exchange.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = Data(await exchange.Content.ReadAsStringAsync());
        auth.GetProperty("user").GetProperty("email").GetString().Should().Be("gina@example.test");
        auth.GetProperty("user").GetProperty("firstName").GetString().Should().Be("Gina");
        auth.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("Student");
        auth.GetProperty("user").GetProperty("permissions").EnumerateArray().Select(r => r.GetString()).Should().Equal("Document.View");

        // The tokens are the ordinary ones: /me works, refresh works, admin surfaces stay closed.
        var api = s.Factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("accessToken").GetString());
        (await api.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.GetAsync("/api/admin/classification/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s.Factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signing_in_again_reuses_the_same_account_and_never_creates_duplicates()
    {
        await using var s = await CreateAsync();
        s.Google.Register("c1", Identity("sub-dup", "dup@example.test"));
        s.Google.Register("c2", Identity("sub-dup", "dup@example.test"));

        var first = Data(await (await s.ExchangeAsync(TicketOf(await s.SignInAsync("c1")))).Content.ReadAsStringAsync());
        var second = Data(await (await s.ExchangeAsync(TicketOf(await s.SignInAsync("c2")))).Content.ReadAsStringAsync());

        second.GetProperty("user").GetProperty("id").GetGuid().Should().Be(first.GetProperty("user").GetProperty("id").GetGuid());
        (await s.Harness.WithDbAsync(db => db.Users.CountAsync(u => u.Email == "dup@example.test"))).Should().Be(1);
    }

    [Fact]
    public async Task A_google_only_account_has_no_password_login()
    {
        await using var s = await CreateAsync();
        s.Google.Register("c", Identity("sub-nopw", "nopw@example.test"));
        (await s.ExchangeAsync(TicketOf(await s.SignInAsync("c")))).EnsureSuccessStatusCode();

        foreach (var guess in new[] { "password", "Passw0rd!", "google", "x" })
        {
            (await s.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "nopw@example.test", password = guess }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Return_urls_are_restricted_to_local_paths()
    {
        await using var s = await CreateAsync();
        var n = 0;
        foreach (var (given, expected) in new[]
                 {
                     ("//evil.example/x", "/dashboard"), ("https://evil.example", "/dashboard"), ("/\\evil", "/dashboard"),
                     ("javascript:alert(1)", "/dashboard"), ("/favorites", "/favorites"),
                 })
        {
            s.Google.Register($"r{n}", Identity($"sub-r{n}", $"r{n}@example.test"));
            var location = await s.SignInAsync($"r{n++}", given);
            QueryOf(new Uri(location))["returnUrl"].Should().Be(expected, $"for '{given}'");
        }
    }

    // ------------------------------------------------------------------ failure modes

    [Fact]
    public async Task A_callback_with_a_wrong_missing_replayed_or_tampered_state_is_rejected()
    {
        await using var s = await CreateAsync();
        s.Google.Register("good", Identity("sub-s", "state@example.test"));

        // wrong state
        var browser = s.Browser();
        await browser.GetAsync("/api/auth/google/start");
        (await browser.GetAsync("/api/auth/google/callback?code=good&state=forged")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/login?error=google_state");

        // no cookie at all (CSRF / login-fixation attempt from another browser)
        var start = await s.Browser().GetAsync("/api/auth/google/start");
        var state = QueryOf(start.Headers.Location!)["state"];
        (await s.Browser().GetAsync($"/api/auth/google/callback?code=good&state={state}")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/login?error=google_state");

        // replay: the flow cookie is single-use
        var victim = s.Browser();
        var startV = await victim.GetAsync("/api/auth/google/start");
        var stateV = QueryOf(startV.Headers.Location!)["state"];
        (await victim.GetAsync($"/api/auth/google/callback?code=good&state={stateV}")).Headers.Location!.ToString()
            .Should().Contain("/auth/google/complete");
        (await victim.GetAsync($"/api/auth/google/callback?code=good&state={stateV}")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/login?error=google_state");

        // tampered cookie
        var tamper = s.Browser();
        var startT = await tamper.GetAsync("/api/auth/google/start");
        var stateT = QueryOf(startT.Headers.Location!)["state"];
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/google/callback?code=good&state={stateT}");
        request.Headers.Add("Cookie", "g_oauth=AAAA-not-a-protected-payload");
        (await s.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false })
                .SendAsync(request)).Headers.Location!.ToString().Should().Be($"{Frontend}/login?error=google_state");
    }

    [Fact]
    public async Task The_authorize_redirect_carries_state_nonce_and_an_s256_pkce_challenge()
    {
        await using var s = await CreateAsync();
        var start = await s.Browser().GetAsync("/api/auth/google/start");
        var q = QueryOf(start.Headers.Location!);

        q["state"].Length.Should().BeGreaterThan(30);
        q["nonce"].Length.Should().BeGreaterThan(30);
        q["code_challenge_method"].Should().Be("S256");
        q["code_challenge"].Length.Should().Be(43, "base64url(SHA-256)");
        start.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant().Should().Contain("httponly").And.Contain("samesite=lax");
    }

    [Fact]
    public async Task User_cancellation_and_exchange_failures_return_to_login_without_an_account()
    {
        await using var s = await CreateAsync();

        var cancelled = s.Browser();
        var start = await cancelled.GetAsync("/api/auth/google/start");
        var state = QueryOf(start.Headers.Location!)["state"];
        (await cancelled.GetAsync($"/api/auth/google/callback?error=access_denied&state={state}")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/login?error=google_cancelled");

        (await s.SignInAsync("unknown-code")).Should().Be($"{Frontend}/login?error=google_failed");
        (await s.Harness.WithDbAsync(db => db.Users.CountAsync(u => u.Email!.Contains("google")))).Should().Be(0);
    }

    [Fact]
    public async Task An_unverified_google_email_is_never_trusted()
    {
        await using var s = await CreateAsync();
        s.Google.Register("c", Identity("sub-unv", "unverified@example.test", verified: false));

        (await s.SignInAsync("c")).Should().Be($"{Frontend}/login?error=google_unverified");
        (await s.Harness.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == "unverified@example.test"))).Should().BeFalse();
    }

    [Fact]
    public async Task Tickets_are_single_use_bound_to_purpose_and_garbage_is_rejected()
    {
        await using var s = await CreateAsync();
        s.Google.Register("c", Identity("sub-t", "ticket@example.test"));
        var ticket = TicketOf(await s.SignInAsync("c"));

        (await s.ExchangeAsync(ticket)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await s.ExchangeAsync(ticket)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await s.ExchangeAsync("garbage")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await s.ExchangeAsync(new string('x', 500))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A link ticket (issued to a signed-in user) cannot be used to log in as that user.
        var (student, _) = await s.Factory.NewStudentClientAsync();
        var link = Data(await (await student.PostAsync("/api/auth/google/link", null)).Content.ReadAsStringAsync());
        var linkTicket = QueryOf(new Uri(Frontend + link.GetProperty("url").GetString()))["link"];
        (await s.ExchangeAsync(linkTicket)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ account linking / privilege safety

    [Fact]
    public async Task A_password_account_with_an_unconfirmed_email_is_not_taken_over_by_google()
    {
        await using var s = await CreateAsync();
        var (_, email) = await s.Factory.NewStudentClientAsync(); // registered with a password, email NOT confirmed
        s.Google.Register("c", Identity("sub-takeover", email));

        (await s.SignInAsync("c")).Should().Be($"{Frontend}/login?error=google_link_required");
        (await s.Harness.WithDbAsync(db => db.UserLogins.AnyAsync())).Should().BeFalse("nothing was linked");
        (await s.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "passw0rd" }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the original sign-in method is untouched");
    }

    [Fact]
    public async Task A_confirmed_non_privileged_account_is_linked_to_the_same_user()
    {
        await using var s = await CreateAsync();
        var (student, email) = await s.Factory.NewStudentClientAsync();
        var userId = Data(await student.GetStringAsync("/api/me")).GetProperty("id").GetGuid();
        await s.Harness.WithDbAsync(db => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailConfirmed, true)));
        s.Google.Register("c", Identity("sub-link", email));

        var auth = Data(await (await s.ExchangeAsync(TicketOf(await s.SignInAsync("c")))).Content.ReadAsStringAsync());

        auth.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        (await s.Harness.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email))).Should().Be(1);
    }

    [Fact]
    public async Task Google_never_signs_anyone_into_an_admin_or_moderator_account_by_email_match()
    {
        await using var s = await CreateAsync();
        s.Google.Register("a", Identity("sub-admin", ApiFactory.AdminEmail));
        s.Google.Register("m", Identity("sub-mod", "moderator@example.local"));

        (await s.SignInAsync("a")).Should().Be($"{Frontend}/login?error=google_link_required");
        (await s.SignInAsync("m")).Should().Be($"{Frontend}/login?error=google_link_required");
        (await s.Harness.WithDbAsync(db => db.UserLogins.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task Suspended_accounts_cannot_sign_in_with_google_or_redeem_an_outstanding_ticket()
    {
        await using var s = await CreateAsync();
        s.Google.Register("c1", Identity("sub-susp", "susp@example.test"));
        s.Google.Register("c2", Identity("sub-susp", "susp@example.test"));
        var ticket = TicketOf(await s.SignInAsync("c1"));
        var auth = Data(await (await s.ExchangeAsync(ticket)).Content.ReadAsStringAsync());
        var userId = auth.GetProperty("user").GetProperty("id").GetGuid();

        // Ticket issued while active, redeemed after suspension → refused.
        var pending = TicketOf(await s.SignInAsync("c2"));
        (await s.Harness.Admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { reason = "x" })).EnsureSuccessStatusCode();
        (await s.ExchangeAsync(pending)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        s.Google.Register("c3", Identity("sub-susp", "susp@example.test"));
        (await s.SignInAsync("c3")).Should().Be($"{Frontend}/login?error=account_disabled");
    }

    [Fact]
    public async Task A_signed_in_user_can_link_google_from_the_profile_and_then_sign_in_with_it()
    {
        await using var s = await CreateAsync();
        var (student, _) = await s.Factory.NewStudentClientAsync();
        var userId = Data(await student.GetStringAsync("/api/me")).GetProperty("id").GetGuid();
        s.Google.Register("link", Identity("sub-profile", "someone.else@gmail.test")); // a different address is fine

        Data(await student.GetStringAsync("/api/auth/google/status")).GetProperty("linked").GetBoolean().Should().BeFalse();

        var start = Data(await (await student.PostAsync("/api/auth/google/link", null)).Content.ReadAsStringAsync()).GetProperty("url").GetString()!;
        var browser = s.Browser();
        var go = await browser.GetAsync(start);
        go.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var state = QueryOf(go.Headers.Location!)["state"];
        var done = await browser.GetAsync($"/api/auth/google/callback?code=link&state={state}");
        done.Headers.Location!.ToString().Should().Be($"{Frontend}/profile?google=linked");

        Data(await student.GetStringAsync("/api/auth/google/status")).GetProperty("linked").GetBoolean().Should().BeTrue();

        // The link ticket is single-use.
        (await s.Browser().GetAsync(start)).Headers.Location!.ToString().Should().Be($"{Frontend}/profile?google=link_expired");

        // Now Google signs in as that very account — still a plain student.
        s.Google.Register("again", Identity("sub-profile", "someone.else@gmail.test"));
        var auth = Data(await (await s.ExchangeAsync(TicketOf(await s.SignInAsync("again")))).Content.ReadAsStringAsync());
        auth.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task A_google_identity_already_linked_elsewhere_cannot_be_linked_to_a_second_account()
    {
        await using var s = await CreateAsync();
        s.Google.Register("first", Identity("sub-shared", "owner@example.test"));
        (await s.ExchangeAsync(TicketOf(await s.SignInAsync("first")))).EnsureSuccessStatusCode();

        var (other, _) = await s.Factory.NewStudentClientAsync();
        var start = Data(await (await other.PostAsync("/api/auth/google/link", null)).Content.ReadAsStringAsync()).GetProperty("url").GetString()!;
        s.Google.Register("second", Identity("sub-shared", "owner@example.test"));
        var browser = s.Browser();
        var state = QueryOf((await browser.GetAsync(start)).Headers.Location!)["state"];

        (await browser.GetAsync($"/api/auth/google/callback?code=second&state={state}")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/profile?google=in_use");
    }

    [Fact]
    public async Task Linking_requires_authentication_and_a_verified_google_email()
    {
        await using var s = await CreateAsync();
        (await s.Factory.CreateClient().PostAsync("/api/auth/google/link", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await s.Factory.CreateClient().GetAsync("/api/auth/google/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (student, _) = await s.Factory.NewStudentClientAsync();
        s.Google.Register("unv", Identity("sub-unv-link", "x@example.test", verified: false));
        var start = Data(await (await student.PostAsync("/api/auth/google/link", null)).Content.ReadAsStringAsync()).GetProperty("url").GetString()!;
        var browser = s.Browser();
        var state = QueryOf((await browser.GetAsync(start)).Headers.Location!)["state"];
        (await browser.GetAsync($"/api/auth/google/callback?code=unv&state={state}")).Headers.Location!.ToString()
            .Should().Be($"{Frontend}/profile?google=unverified");
    }

    [Fact]
    public async Task Google_sign_in_triggers_the_login_prompt_like_a_password_login()
    {
        await using var s = await CreateAsync();
        await s.Harness.UpdateSettingsAsync(x => x["downloadTriggerEnabled"] = false);
        await s.Harness.UploadUnclassifiedAsync();
        s.Google.Register("c", Identity("sub-prompt", "prompt@example.test"));
        var auth = Data(await (await s.ExchangeAsync(TicketOf(await s.SignInAsync("c")))).Content.ReadAsStringAsync());

        var api = s.Factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("accessToken").GetString());
        var prompt = Data(await api.GetStringAsync("/api/classification/prompt"));
        prompt.GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();
        prompt.GetProperty("reason").GetString().Should().Be("login");
    }
}
