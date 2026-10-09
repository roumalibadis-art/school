namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using USTHBStudy.Application.Auth.Google;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>
/// Real concurrency against real MySQL (spec test 17): parallel HTTP requests racing for the same slot, vote,
/// quota allowance or proposal. SQLite's single connection cannot exercise this, so these need USTHB_TEST_MYSQL.
/// </summary>
public class MySqlConcurrencyTests
{
    private static JsonElement Data(string json) => ClassificationHarness.Data(json);

    private static async Task<ClassificationHarness> CreateAsync(Action<Dictionary<string, object?>>? settings = null, MySqlApiFactory? factory = null) =>
        await ClassificationHarness.CreateAsync(settings, factory ?? new MySqlApiFactory());

    [MySqlFact]
    public async Task Parallel_duplicate_submissions_of_one_vote_record_exactly_one_vote()
    {
        await using var h = await CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var spec = await h.CreateSpecialtyAsync("Spec");
        var voter = await h.NewVoterAsync();
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            voter.VoteAsync(assignment, new { decision = "Classify", specialtyId = spec, documentType = "Exam" })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK).Select(r => r.StatusCode)
            .Should().OnlyContain(c => c == HttpStatusCode.Conflict, "every loser is told it already voted — never a 500");
        (await h.WithDbAsync(db => db.ClassificationVotes.CountAsync(v => v.DocumentId == docId))).Should().Be(1);
        (await voter.MeAsync()).GetProperty("validContributions").GetInt32().Should().Be(1);
    }

    [MySqlFact]
    public async Task Parallel_task_requests_never_assign_a_document_to_more_than_the_required_voters()
    {
        await using var h = await CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var voters = await h.NewVotersAsync(10);

        var results = await Task.WhenAll(voters.Select(async v =>
        {
            var response = await v.Client.PostAsync("/api/classification/tasks/next", null);
            return response.StatusCode;
        }));

        results.Should().OnlyContain(c => c == HttpStatusCode.OK);
        var assigned = await h.WithDbAsync(db => db.ClassificationAssignments
            .Where(a => a.DocumentId == docId).Select(a => a.UserId).ToListAsync());
        assigned.Should().HaveCount(3, "exactly RequiredVoters slots exist");
        assigned.Should().OnlyHaveUniqueItems();
    }

    [MySqlFact]
    public async Task Required_voters_voting_at_the_same_instant_yield_one_consistent_outcome()
    {
        await using var h = await CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var spec = await h.CreateSpecialtyAsync("Spec");
        var voters = await h.NewVotersAsync(3);
        var assignments = new List<(Voter Voter, Guid Assignment)>();
        foreach (var v in voters)
        {
            assignments.Add((v, (await v.TaskAsync()).Single().AssignmentId));
        }

        var responses = await Task.WhenAll(assignments.Select(a =>
            a.Voter.VoteAsync(a.Assignment, new { decision = "Classify", specialtyId = spec, documentType = "Exam" })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var doc = await h.GetClassificationDetailAsync(docId);
        doc.GetProperty("summary").GetProperty("verification").GetString().Should().Be("Verified");
        doc.GetProperty("votes").GetArrayLength().Should().Be(3);
        doc.GetProperty("history").EnumerateArray()
            .Count(e => e.GetProperty("action").GetString() == "classification.verified").Should().Be(1, "the outcome is applied once");
        foreach (var v in voters)
        {
            var me = await v.MeAsync();
            me.GetProperty("resolvedVotes").GetInt32().Should().Be(1);
            me.GetProperty("agreedVotes").GetInt32().Should().Be(1);
        }
    }

    [MySqlFact]
    public async Task Parallel_downloads_can_never_overspend_the_quota()
    {
        await using var h = await CreateAsync(s =>
        {
            s["quotaEnabled"] = true;
            s["freeDownloadsPerWindow"] = 3;
        });
        var (_, slug) = await h.PublishDocumentAsync();
        var voter = await h.NewVoterAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => voter.DownloadAsync(slug)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(3);
        responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden).Should().Be(9);
        (await voter.MeAsync()).GetProperty("quota").GetProperty("used").GetInt32().Should().Be(3);
    }

    [MySqlFact]
    public async Task Parallel_identical_proposals_collapse_into_one()
    {
        await using var h = await CreateAsync();
        var voters = await h.NewVotersAsync(8);

        var responses = await Task.WhenAll(voters.Select(v =>
            v.Client.PostAsJsonAsync("/api/classification/proposals", new { category = "Session", value = "Session spéciale" })));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var ids = new HashSet<Guid>();
        foreach (var r in responses)
        {
            ids.Add(Data(await r.Content.ReadAsStringAsync()).GetProperty("proposal").GetProperty("id").GetGuid());
        }

        ids.Should().HaveCount(1);
        (await h.WithDbAsync(db => db.TaxonomyProposals.CountAsync())).Should().Be(1);
    }

    [MySqlFact]
    public async Task A_google_sign_in_ticket_can_be_redeemed_by_only_one_of_many_parallel_requests()
    {
        var google = new FakeGoogleOidcClient();
        var factory = new MySqlApiFactory
        {
            ConfigureExtraServices = s =>
            {
                s.RemoveAll<IGoogleOidcClient>();
                s.AddSingleton<IGoogleOidcClient>(google);
            },
        };
        await using var h = await CreateAsync(factory: factory);
        google.Register("c", new GoogleIdentity("sub-race", "race@example.test", true, "R", "R"));
        var browser = h.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var start = await browser.GetAsync("/api/auth/google/start");
        var state = System.Web.HttpUtility.ParseQueryString(start.Headers.Location!.Query)["state"];
        var callback = await browser.GetAsync($"/api/auth/google/callback?code=c&state={state}");
        var ticket = System.Web.HttpUtility.ParseQueryString(callback.Headers.Location!.Query)["ticket"];

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            h.Factory.CreateClient().PostAsJsonAsync("/api/auth/google/exchange", new { ticket })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(7);
    }

    [MySqlFact]
    public async Task Heavy_mixed_load_keeps_every_invariant()
    {
        await using var h = await CreateAsync(s => s["documentsPerTask"] = 5);
        var spec = await h.CreateSpecialtyAsync("Spec");
        var docs = await h.UploadUnclassifiedAsync(5);
        var voters = await h.NewVotersAsync(9);

        // Every voter concurrently asks for a task and then answers all of it concurrently.
        var statuses = (await Task.WhenAll(voters.Select(async v =>
        {
            var task = await v.TaskAsync();
            var votes = await Task.WhenAll(task.Select(t =>
                v.VoteAsync(t.AssignmentId, new { decision = "Classify", specialtyId = spec, documentType = "Exam" })));
            return votes.Select(r => r.StatusCode).ToList();
        }))).SelectMany(x => x).ToList();

        statuses.Should().OnlyContain(c => c == HttpStatusCode.OK || c == HttpStatusCode.Conflict, "no request may fail with a 5xx");

        var (votesPerDoc, assignmentsPerDoc, verified, totalVotes) = await h.WithDbAsync(async db => (
            await db.ClassificationVotes.GroupBy(v => v.DocumentId).Select(g => g.Count()).ToListAsync(),
            await db.ClassificationAssignments.Where(a => a.Status != USTHBStudy.Domain.Classification.AssignmentStatus.Expired)
                .GroupBy(a => a.DocumentId).Select(g => g.Count()).ToListAsync(),
            await db.Documents.CountAsync(d => d.VerificationStatus == USTHBStudy.Domain.Classification.VerificationStatus.Verified),
            await db.ClassificationVotes.CountAsync()));

        votesPerDoc.Should().OnlyContain(c => c <= 3, "no document may collect more than RequiredVoters votes");
        assignmentsPerDoc.Should().OnlyContain(c => c <= 3);
        verified.Should().Be(votesPerDoc.Count(c => c == 3), "every fully-voted document was resolved exactly once");

        var contributions = 0;
        foreach (var v in voters)
        {
            contributions += (await v.MeAsync()).GetProperty("validContributions").GetInt32();
        }

        contributions.Should().Be(totalVotes, "rewards and recorded votes never diverge");
        _ = docs;
    }
}
