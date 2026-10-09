namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>Prompt triggers and contribution-based download access (spec tests 12–14).</summary>
public class ContributionAccessTests
{
    private static JsonElement Data(string json) => ClassificationHarness.Data(json);

    private static object Vote(Guid specialtyId) => new { decision = "Classify", specialtyId, documentType = "Exam" };

    // ------------------------------------------------------------------ triggers (test 12)

    [Fact]
    public async Task Login_trigger_prompts_once_per_login_and_never_when_nothing_needs_classifying()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["downloadTriggerEnabled"] = false);
        var voter = await h.NewVoterAsync();
        (await voter.LoginAgainAsync()).EnsureSuccessStatusCode();

        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse("the queue is empty");

        await h.UploadUnclassifiedAsync(2);
        var prompt = await voter.PromptAsync();
        prompt.GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();
        prompt.GetProperty("reason").GetString().Should().Be("login");
        prompt.GetProperty("documentsPerTask").GetInt32().Should().Be(3);

        // Reading the prompt has no side effects; answering it consumes the trigger.
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();
        var started = await voter.Client.PostAsJsonAsync("/api/classification/prompt/ack", new { action = "start" });
        Data(await started.Content.ReadAsStringAsync()).GetProperty("items").GetArrayLength().Should().Be(2);

        var afterStart = await voter.PromptAsync();
        afterStart.GetProperty("shouldPrompt").GetBoolean().Should().BeFalse("an open task exists — never prompt twice");
        afterStart.GetProperty("hasOpenTask").GetBoolean().Should().BeTrue();
        afterStart.GetProperty("openTaskRemaining").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Login_trigger_can_be_disabled_by_the_administrator()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["loginTriggerEnabled"] = false;
            s["downloadTriggerEnabled"] = false;
        });
        await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        (await voter.LoginAgainAsync()).EnsureSuccessStatusCode();

        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Choosing_later_snoozes_the_prompt_and_a_new_login_after_completing_prompts_again()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["downloadTriggerEnabled"] = false);
        await h.UploadUnclassifiedAsync(2);
        var voter = await h.NewVoterAsync();
        (await voter.LoginAgainAsync()).EnsureSuccessStatusCode();
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();

        var later = await voter.Client.PostAsJsonAsync("/api/classification/prompt/ack", new { action = "later" });
        Data(await later.Content.ReadAsStringAsync()).ValueKind.Should().Be(JsonValueKind.Null);
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse();

        // Even a fresh login does not re-interrupt inside the snooze window.
        (await voter.LoginAgainAsync()).EnsureSuccessStatusCode();
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse();

        (await voter.Client.PostAsJsonAsync("/api/classification/prompt/ack", new { action = "bogus" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_trigger_fires_after_the_configured_number_of_downloads()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["loginTriggerEnabled"] = false;
            s["downloadsPerPrompt"] = 3;
        });
        await h.UploadUnclassifiedAsync(2);
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();

        for (var i = 0; i < 2; i++)
        {
            (await voter.DownloadAsync(slug)).EnsureSuccessStatusCode();
        }

        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse("2 < 3");
        (await voter.DownloadAsync(slug)).EnsureSuccessStatusCode();
        var prompt = await voter.PromptAsync();
        prompt.GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();
        prompt.GetProperty("reason").GetString().Should().Be("downloads");

        await voter.Client.PostAsJsonAsync("/api/classification/prompt/ack", new { action = "later" });
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse("counter was reset by the answer");
    }

    [Fact]
    public async Task Combined_triggers_produce_a_single_prompt_that_consumes_both()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["downloadsPerPrompt"] = 1);
        await h.UploadUnclassifiedAsync(3);
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();
        (await voter.LoginAgainAsync()).EnsureSuccessStatusCode();
        (await voter.DownloadAsync(slug)).EnsureSuccessStatusCode();

        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeTrue();
        await voter.Client.PostAsJsonAsync("/api/classification/prompt/ack", new { action = "later" });

        // Neither the login stamp nor the download counter can re-trigger until something new happens.
        (await voter.PromptAsync()).GetProperty("shouldPrompt").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Total_downloads_are_counted_only_by_the_server()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();
        (await voter.DownloadAsync(slug)).EnsureSuccessStatusCode();
        (await voter.DownloadAsync(slug)).EnsureSuccessStatusCode();

        (await voter.MeAsync()).GetProperty("quota").GetProperty("totalDownloads").GetInt64().Should().Be(2);

        // A forged or replayed file link neither downloads nor counts.
        (await h.Factory.CreateClient().GetAsync("/api/files?t=forged")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await voter.MeAsync()).GetProperty("quota").GetProperty("totalDownloads").GetInt64().Should().Be(2);
    }

    // ------------------------------------------------------------------ rewards (test 13)

    [Fact]
    public async Task Downloads_are_unlimited_until_the_administrator_turns_the_quota_on()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();
        for (var i = 0; i < 30; i++)
        {
            (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await voter.MeAsync()).GetProperty("quota").GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Valid_contributions_earn_extra_downloads_and_the_limit_is_explained_before_it_blocks()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 2;
            s["bonusDownloadsPerContribution"] = 2;
            s["loginTriggerEnabled"] = false;
        });
        var spec = await h.CreateSpecialtyAsync("Spec");
        await h.UploadUnclassifiedAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();

        // Before anything is restricted, the user can see exactly where they stand and how to earn more.
        var quota = (await voter.MeAsync()).GetProperty("quota");
        quota.GetProperty("allowed").GetInt32().Should().Be(2);
        quota.GetProperty("remaining").GetInt32().Should().Be(2);
        quota.GetProperty("bonusPerContribution").GetInt32().Should().Be(2);
        quota.GetProperty("documentsPerTask").GetInt32().Should().Be(3);

        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);

        var blocked = await voter.DownloadAsync(slug);
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).Should().Contain("contribution_required");
        body.GetProperty("message").GetString().Should().Contain("classify");

        // Contribute → earn two more downloads.
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;
        var vote = await ClassificationHarness.DataAsync(await voter.VoteAsync(assignment, Vote(spec)));
        vote.GetProperty("rewarded").GetBoolean().Should().BeTrue();
        vote.GetProperty("bonusDownloadsGranted").GetInt32().Should().Be(2);
        vote.GetProperty("quota").GetProperty("remaining").GetInt32().Should().Be(2);

        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Skipping_and_repeating_do_not_earn_downloads_and_the_bonus_is_capped()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 1;
            s["bonusDownloadsPerContribution"] = 2;
            s["maxBonusPerWindow"] = 3;
        });
        var spec = await h.CreateSpecialtyAsync("Spec");
        await h.UploadUnclassifiedAsync(3);
        var voter = await h.NewVoterAsync();

        var items = await voter.TaskAsync();
        (await voter.SkipAsync(items[0].AssignmentId)).EnsureSuccessStatusCode();
        (await voter.MeAsync()).GetProperty("quota").GetProperty("bonusEarned").GetInt32().Should().Be(0);

        (await voter.VoteAsync(items[1].AssignmentId, Vote(spec))).EnsureSuccessStatusCode();
        (await voter.VoteAsync(items[1].AssignmentId, Vote(spec))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await voter.MeAsync()).GetProperty("quota").GetProperty("bonusEarned").GetInt32().Should().Be(2);

        (await voter.VoteAsync(items[2].AssignmentId, Vote(spec))).EnsureSuccessStatusCode();
        var quota = (await voter.MeAsync()).GetProperty("quota");
        quota.GetProperty("bonusEarned").GetInt32().Should().Be(3, "2 + 2 is capped at maxBonusPerWindow");
        quota.GetProperty("allowed").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task The_daily_cap_stops_rewards_but_still_records_the_vote()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["maxRewardedContributionsPerDay"] = 1;
        });
        var spec = await h.CreateSpecialtyAsync("Spec");
        await h.UploadUnclassifiedAsync(2);
        var voter = await h.NewVoterAsync();
        var items = await voter.TaskAsync();

        var first = await ClassificationHarness.DataAsync(await voter.VoteAsync(items[0].AssignmentId, Vote(spec)));
        var second = await ClassificationHarness.DataAsync(await voter.VoteAsync(items[1].AssignmentId, Vote(spec)));

        first.GetProperty("rewarded").GetBoolean().Should().BeTrue();
        second.GetProperty("rewarded").GetBoolean().Should().BeFalse();
        (await h.WithDbAsync(db => db.ClassificationVotes.CountAsync(v => v.UserId == voter.Id))).Should().Be(2);
        (await voter.MeAsync()).GetProperty("validContributions").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_quota_window_resets_after_the_configured_period()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 1;
            s["quotaWindowDays"] = 30;
        });
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();
        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await h.WithDbAsync(db => db.ContributionStats.Where(s => s.UserId == voter.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.QuotaWindowStart, DateTime.UtcNow.AddDays(-31))));

        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Premium_members_and_staff_are_never_limited_by_the_quota()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 1;
        });
        var (_, slug) = await h.PublishDocumentAsync();
        var member = await h.NewVoterAsync();
        (await h.Admin.PostAsJsonAsync($"/api/admin/users/{member.Id}/premium", new { months = 1 })).EnsureSuccessStatusCode();

        for (var i = 0; i < 4; i++)
        {
            (await member.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await h.Admin.GetAsync($"/api/documents/{slug}/download")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // The UI is told so, and never shows them an allowance counter.
        (await member.MeAsync()).GetProperty("quota").GetProperty("exempt").GetBoolean().Should().BeTrue();
        (await new Voter(h.Admin, "admin", Guid.Empty).MeAsync()).GetProperty("quota").GetProperty("exempt").GetBoolean().Should().BeTrue();
        var free = await h.NewVoterAsync();
        (await free.MeAsync()).GetProperty("quota").GetProperty("exempt").GetBoolean().Should().BeFalse();
    }

    // ------------------------------------------------------------------ no bypass (test 14)

    [Fact]
    public async Task Earning_contribution_credit_never_unlocks_premium_documents_or_anonymous_access()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 100;
            s["bonusDownloadsPerContribution"] = 100;
        });
        var spec = await h.CreateSpecialtyAsync("Spec");
        await h.UploadUnclassifiedAsync();
        var (_, premiumSlug) = await h.PublishDocumentAsync(premium: true);
        var voter = await h.NewVoterAsync();
        (await voter.VoteAsync((await voter.TaskAsync()).Single().AssignmentId, Vote(spec))).EnsureSuccessStatusCode();

        (await voter.DownloadAsync(premiumSlug)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await h.Factory.CreateClient().GetAsync($"/api/documents/{premiumSlug}/download")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await voter.MeAsync()).GetProperty("quota").GetProperty("totalDownloads").GetInt64()
            .Should().Be(0, "a refused download is not counted");
    }

    [Fact]
    public async Task Suspended_users_cannot_download_even_with_credit()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();
        (await h.Admin.PostAsJsonAsync($"/api/admin/users/{voter.Id}/suspend", new { reason = "test" })).EnsureSuccessStatusCode();

        (await voter.DownloadAsync(slug)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_tampered_vote_cannot_claim_a_reward_or_other_peoples_assignments()
    {
        await using var h = await ClassificationHarness.CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["maxRewardedContributionsPerDay"] = 1;
        });
        var spec = await h.CreateSpecialtyAsync("Spec");
        await h.UploadUnclassifiedAsync(2);
        var voter = await h.NewVoterAsync();
        var items = await voter.TaskAsync();
        (await voter.VoteAsync(items[0].AssignmentId, Vote(spec))).EnsureSuccessStatusCode();

        // Extra client-supplied properties are ignored; the server decides who is rewarded.
        var forged = await voter.VoteAsync(items[1].AssignmentId, new
        {
            decision = "Classify", specialtyId = spec, documentType = "Exam",
            rewarded = true, bonusDownloadsGranted = 9999, userId = Guid.NewGuid(),
        });
        (await ClassificationHarness.DataAsync(forged)).GetProperty("rewarded").GetBoolean().Should().BeFalse();
        (await voter.MeAsync()).GetProperty("quota").GetProperty("bonusEarned").GetInt32().Should().BeLessThan(10);
    }

    [Fact]
    public async Task All_classification_endpoints_require_authentication()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var anon = h.Factory.CreateClient();
        foreach (var path in new[] { "/api/classification/prompt", "/api/classification/options", "/api/classification/me" })
        {
            (await anon.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }

        (await anon.PostAsync("/api/classification/tasks/next", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsJsonAsync($"/api/classification/assignments/{Guid.NewGuid()}/vote", new { decision = "Classify" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Settings_are_validated_audited_and_admin_only()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var student = await h.NewVoterAsync();
        (await student.Client.GetAsync("/api/admin/classification/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.Client.GetAsync("/api/admin/classification/report")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.Client.GetAsync("/api/admin/classification/documents")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        foreach (var bad in new Action<Dictionary<string, object?>>[]
                 {
                     s => s["documentsPerTask"] = 0,
                     s => s["requiredVoters"] = 0,
                     s => s["agreementPercent"] = 50,
                     s => s["nonEducationalPolicy"] = "Explode",
                     s => s["requiredFields"] = new[] { "Nope" },
                     s => s["downloadsPerPrompt"] = 0,
                 })
        {
            (await h.TryUpdateSettingsAsync(bad)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await h.TryUpdateSettingsAsync(s => s["documentsPerTask"] = 5)).EnsureSuccessStatusCode();
        var audit = Data(await h.Admin.GetStringAsync("/api/admin/audit?action=classification.settings_updated"));
        audit.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_report_counts_documents_votes_proposals_and_contributions()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var spec = await h.CreateSpecialtyAsync("Spec");
        var voted = await h.UploadUnclassifiedAsync();
        await h.UploadUnclassifiedAsync(); // stays untouched → still awaiting votes
        var voters = await h.NewVotersAsync(3);
        foreach (var v in voters)
        {
            var items = await v.TaskAsync();
            var assignment = items.Single(i => i.DocumentId == voted).AssignmentId;
            (await v.VoteAsync(assignment, Vote(spec))).EnsureSuccessStatusCode();
        }

        await voters[0].Client.PostAsJsonAsync("/api/classification/proposals", new { category = "Session", value = "Une session" });

        var report = Data(await h.Admin.GetStringAsync("/api/admin/classification/report"));
        report.GetProperty("verified").GetInt64().Should().Be(1);
        report.GetProperty("awaitingVotes").GetInt64().Should().Be(1);
        report.GetProperty("totalVotes").GetInt64().Should().Be(3);
        report.GetProperty("validContributions").GetInt64().Should().Be(3);
        report.GetProperty("votingAgreementRate").GetDouble().Should().Be(100);
        report.GetProperty("proposalsPending").GetInt64().Should().Be(1);
        report.GetProperty("tasksAssigned").GetInt64().Should().Be(3);
        report.GetProperty("activeContributors").GetInt64().Should().Be(3);
    }
}
