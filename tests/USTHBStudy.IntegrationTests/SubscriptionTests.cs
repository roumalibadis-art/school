namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

public class SubscriptionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SubscriptionTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string json) => JsonDocument.Parse(json).RootElement.GetProperty("data");

    private static async Task<Guid> CreatePlan(HttpClient admin, int durationDays = 30, decimal price = 500)
    {
        var res = await admin.PostAsJsonAsync("/api/admin/subscription-plans", new
        {
            name = $"Plan {Guid.NewGuid():N}",
            durationDays,
            price,
            currency = "DZD",
            features = "Docs Premium\nCorrigés",
            isActive = true,
            displayOrder = 1,
        });
        res.EnsureSuccessStatusCode();
        return Data(await res.Content.ReadAsStringAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Slug)> PublishPremiumDoc(HttpClient admin)
    {
        var moduleId = await admin.CreateModuleAsync();
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(SamplePdf.OnePage()), "File", "d.pdf" },
            { new StringContent($"Premium {Guid.NewGuid():N}"), "Title" },
            { new StringContent("Exam"), "Type" },
            { new StringContent(moduleId.ToString()), "ModuleId" },
            { new StringContent("true"), "IsPremium" },
        };
        var up = await admin.PostAsync("/api/documents", content);
        up.EnsureSuccessStatusCode();
        var doc = Data(await up.Content.ReadAsStringAsync()).GetProperty("document");
        var id = doc.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/documents/{id}/status", new { status = "Published" })).EnsureSuccessStatusCode();
        return (id, doc.GetProperty("slug").GetString()!);
    }

    [Fact]
    public async Task Full_flow_checkout_pending_then_admin_approval_activates_premium()
    {
        var admin = await _factory.AdminClientAsync();
        var planId = await CreatePlan(admin, durationDays: 60);
        var (_, premiumSlug) = await PublishPremiumDoc(admin);
        var (student, _) = await _factory.NewStudentClientAsync();

        // Plans are public.
        var plans = Data(await (await _factory.CreateClient().GetAsync("/api/subscriptions/plans")).Content.ReadAsStringAsync());
        plans.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Should().Contain(planId);

        // Student checks out — pending, with a reference and instructions.
        var checkout = Data(await (await student.PostAsJsonAsync("/api/subscriptions", new { planId })).Content.ReadAsStringAsync());
        var reference = checkout.GetProperty("reference").GetString()!;
        reference.Should().StartWith("USTHB-");
        checkout.GetProperty("instructions").GetString().Should().Contain(reference);
        checkout.GetProperty("subscription").GetProperty("status").GetString().Should().Be("Pending");

        // Not premium yet — premium download is forbidden.
        (await student.GetAsync($"/api/documents/{premiumSlug}/download")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Admin sees the pending payment and approves it.
        var pending = Data(await student.GetStringAsync("/api/subscriptions/me")).GetProperty("current");
        var paymentId = pending.GetProperty("latestPayment").GetProperty("id").GetGuid();

        var adminList = Data(await admin.GetStringAsync("/api/admin/payments?status=Pending"));
        adminList.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Should().Contain(paymentId);

        var approved = Data(await (await admin.PostAsJsonAsync($"/api/admin/payments/{paymentId}/approve", new { note = "CCP vérifié" })).Content.ReadAsStringAsync());
        approved.GetProperty("status").GetString().Should().Be("Success");

        // Now premium: /api/me reflects it and the premium download works.
        Data(await student.GetStringAsync("/api/me")).GetProperty("isPremium").GetBoolean().Should().BeTrue();
        (await student.GetAsync($"/api/documents/{premiumSlug}/download")).StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = Data(await student.GetStringAsync("/api/subscriptions/me"));
        mine.GetProperty("isPremiumActive").GetBoolean().Should().BeTrue();
        mine.GetProperty("current").GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Rejecting_a_payment_cancels_the_subscription_and_leaves_the_user_free()
    {
        var admin = await _factory.AdminClientAsync();
        var planId = await CreatePlan(admin);
        var (student, _) = await _factory.NewStudentClientAsync();

        var checkout = Data(await (await student.PostAsJsonAsync("/api/subscriptions", new { planId })).Content.ReadAsStringAsync());
        var paymentId = checkout.GetProperty("subscription").GetProperty("latestPayment").GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/admin/payments/{paymentId}/reject", new { note = "Non reçu" })).EnsureSuccessStatusCode();

        var mine = Data(await student.GetStringAsync("/api/subscriptions/me"));
        mine.GetProperty("isPremiumActive").GetBoolean().Should().BeFalse();
        mine.GetProperty("history").EnumerateArray().First().GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task A_second_checkout_while_one_is_pending_is_409()
    {
        var admin = await _factory.AdminClientAsync();
        var planId = await CreatePlan(admin);
        var (student, _) = await _factory.NewStudentClientAsync();

        (await student.PostAsJsonAsync("/api/subscriptions", new { planId })).EnsureSuccessStatusCode();
        var second = await student.PostAsJsonAsync("/api/subscriptions", new { planId });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checking_out_an_unknown_plan_is_404()
    {
        var (student, _) = await _factory.NewStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/subscriptions", new { planId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Students_cannot_manage_plans()
    {
        var (student, _) = await _factory.NewStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/admin/subscription-plans", new
        {
            name = "Hack", durationDays = 30, price = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
