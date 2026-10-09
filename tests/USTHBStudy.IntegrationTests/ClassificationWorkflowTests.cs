namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using USTHBStudy.Domain.Classification;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>Task batching, distinct voters, duplicate votes, skipping and consensus (spec tests 3–9, 17).</summary>
public class ClassificationWorkflowTests
{
    private static object Classify(Guid specialtyId, string type = "Exam", Guid? yearId = null, Guid? sessionId = null) =>
        new { decision = "Classify", specialtyId, documentType = type, academicYearId = yearId, sessionId };

    private static readonly object NotEducational = new { decision = "NotEducational" };

    // ------------------------------------------------------------------ batching (test 3)

    [Fact]
    public async Task Task_size_follows_the_configured_documents_per_task()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["documentsPerTask"] = 2);
        await h.UploadUnclassifiedAsync(5);
        var voter = await h.NewVoterAsync();

        (await voter.TaskAsync()).Should().HaveCount(2);

        await h.UpdateSettingsAsync(s => s["documentsPerTask"] = 4);
        var other = await h.NewVoterAsync();
        (await other.TaskAsync()).Should().HaveCount(4);
    }

    [Fact]
    public async Task Default_task_size_is_three_and_a_short_queue_yields_a_smaller_task()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        (await h.GetSettingsAsync())["documentsPerTask"].Should().Be(3);
        (await h.GetSettingsAsync())["requiredVoters"].Should().Be(3);

        await h.UploadUnclassifiedAsync(1);
        (await (await h.NewVoterAsync()).TaskAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task An_open_task_is_resumed_not_duplicated_and_nothing_to_do_returns_null()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        (await voter.TaskAsync()).Should().BeEmpty("there are no unclassified documents yet");

        await h.UploadUnclassifiedAsync(2);
        var first = await voter.TaskAsync();
        var second = await voter.TaskAsync();
        second.Select(x => x.AssignmentId).Should().BeEquivalentTo(first.Select(x => x.AssignmentId));

        var tasks = await h.WithDbAsync(db => db.ClassificationTasks.CountAsync(t => t.UserId == voter.Id));
        tasks.Should().Be(1);
    }

    [Fact]
    public async Task Documents_already_classified_by_staff_never_enter_the_queue()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.PublishDocumentAsync(); // has a module → Classified
        (await (await h.NewVoterAsync()).TaskAsync()).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ duplicate votes (test 4, 17)

    [Fact]
    public async Task A_user_cannot_vote_twice_on_the_same_document()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var spec = await h.CreateSpecialtyAsync("Spec A");
        var voter = await h.NewVoterAsync();
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;

        (await voter.VoteAsync(assignment, Classify(spec))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await voter.VoteAsync(assignment, Classify(spec))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var votes = await h.WithDbAsync(db => db.ClassificationVotes.CountAsync(v => v.DocumentId == docId && v.UserId == voter.Id));
        votes.Should().Be(1);
        (await voter.MeAsync()).GetProperty("validContributions").GetInt32().Should().Be(1, "a duplicate submit is not rewarded");
    }

    [Fact]
    public async Task The_database_itself_rejects_a_second_vote_for_the_same_document_user_and_round()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;
        (await voter.VoteAsync(assignment, NotEducational)).EnsureSuccessStatusCode();

        var act = () => h.WithDbAsync(async db =>
        {
            db.ClassificationVotes.Add(new ClassificationVote
            {
                DocumentId = docId, UserId = voter.Id, Round = 1, AssignmentId = assignment,
                Decision = VoteDecision.NotEducational, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Someone_elses_assignment_looks_like_it_does_not_exist()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.UploadUnclassifiedAsync();
        var owner = await h.NewVoterAsync();
        var intruder = await h.NewVoterAsync();
        var assignment = (await owner.TaskAsync()).Single().AssignmentId;

        (await intruder.VoteAsync(assignment, NotEducational)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.SkipAsync(assignment)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.Client.GetAsync($"/api/classification/assignments/{assignment}/preview")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_vote_must_carry_something_and_reject_inconsistent_values()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;

        (await voter.VoteAsync(assignment, new { decision = "Classify" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await voter.VoteAsync(assignment, new { decision = "Maybe" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await voter.VoteAsync(assignment, new { decision = "Classify", documentType = "Bogus" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await voter.VoteAsync(assignment, new { decision = "Classify", specialtyId = Guid.NewGuid() })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // Nothing was recorded by the rejected attempts.
        (await h.WithDbAsync(db => db.ClassificationVotes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task The_look_before_you_answer_gate_is_enforced_server_side()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["minSecondsBeforeVote"] = 60);
        await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        var assignment = (await voter.TaskAsync()).Single().AssignmentId;

        (await voter.VoteAsync(assignment, NotEducational)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ distinct voters (test 5)

    [Fact]
    public async Task Each_document_is_assigned_to_exactly_the_required_number_of_distinct_users()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var voters = await h.NewVotersAsync(5);

        var served = new List<Voter>();
        foreach (var voter in voters)
        {
            if ((await voter.TaskAsync()).Any(x => x.DocumentId == docId))
            {
                served.Add(voter);
            }
        }

        served.Should().HaveCount(3, "RequiredVoters defaults to 3 and the rest must wait for the next free slot");
        served.Select(v => v.Id).Should().OnlyHaveUniqueItems();
        (await h.WithDbAsync(db => db.ClassificationAssignments.CountAsync(a => a.DocumentId == docId))).Should().Be(3);
    }

    [Fact]
    public async Task The_required_voter_count_is_configurable()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["requiredVoters"] = 2);
        var docId = await h.UploadUnclassifiedAsync();
        var voters = await h.NewVotersAsync(4);

        var served = 0;
        foreach (var v in voters)
        {
            served += (await v.TaskAsync()).Count(x => x.DocumentId == docId);
        }

        served.Should().Be(2);
    }

    [Fact]
    public async Task A_user_is_never_offered_a_document_they_already_answered_or_uploaded()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        (await voter.VoteAsync((await voter.TaskAsync()).Single().AssignmentId, NotEducational)).EnsureSuccessStatusCode();
        (await voter.TaskAsync()).Should().BeEmpty();

        // The uploader (an admin here) is excluded from classifying their own upload.
        var adminAsVoter = new Voter(h.Admin, "admin", Guid.Empty);
        (await adminAsVoter.TaskAsync()).Should().BeEmpty();
        _ = docId;
    }

    // ------------------------------------------------------------------ skipping (test 9)

    [Fact]
    public async Task Skipping_frees_the_slot_earns_nothing_and_is_never_offered_again_to_the_same_user()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var skipper = await h.NewVoterAsync();
        var assignment = (await skipper.TaskAsync()).Single().AssignmentId;

        var skip = await skipper.SkipAsync(assignment);
        skip.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ClassificationHarness.DataAsync(skip);
        result.GetProperty("taskCompleted").GetBoolean().Should().BeTrue();

        var me = await skipper.MeAsync();
        me.GetProperty("skippedCount").GetInt32().Should().Be(1);
        me.GetProperty("validContributions").GetInt32().Should().Be(0);
        (await skipper.VoteAsync(assignment, NotEducational)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await skipper.TaskAsync()).Should().BeEmpty("skipped documents are not re-offered in the same round");

        // Three other users can still fill all three slots.
        var others = await h.NewVotersAsync(3);
        foreach (var o in others)
        {
            (await o.TaskAsync()).Should().ContainSingle(x => x.DocumentId == docId);
        }
    }

    // ------------------------------------------------------------------ consensus (tests 6–8)

    private static async Task VoteAllAsync(IEnumerable<Voter> voters, params object[] bodies)
    {
        var i = 0;
        foreach (var voter in voters)
        {
            var assignment = (await voter.TaskAsync()).Single().AssignmentId;
            (await voter.VoteAsync(assignment, bodies[i++])).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Unanimous_votes_classify_and_verify_the_document_and_apply_the_values()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var spec = await h.CreateSpecialtyAsync("Informatique");
        var year = await h.CreateAcademicYearAsync(2024);
        var session = await h.CreateSessionAsync("Session normale");
        var voters = await h.NewVotersAsync(3);

        await VoteAllAsync(voters, Classify(spec, "Exam", year, session), Classify(spec, "Exam", year, session), Classify(spec, "Exam", year, session));

        var doc = await h.GetDocumentAsync(docId);
        doc.GetProperty("classificationStatus").GetString().Should().Be("Classified");
        doc.GetProperty("verificationStatus").GetString().Should().Be("Verified");
        doc.GetProperty("type").GetString().Should().Be("Exam");
        doc.GetProperty("academicYearId").GetGuid().Should().Be(year);
        doc.GetProperty("sessionId").GetGuid().Should().Be(session);

        var detail = await h.GetClassificationDetailAsync(docId);
        detail.GetProperty("specialtyId").GetGuid().Should().Be(spec);
        detail.GetProperty("votes").GetArrayLength().Should().Be(3);
        detail.GetProperty("history").EnumerateArray().Select(e => e.GetProperty("action").GetString())
            .Should().Contain("classification.verified");

        // Agreement is tracked per voter for the reports.
        (await voters[0].MeAsync()).GetProperty("agreedVotes").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Two_of_three_agreeing_meets_the_default_threshold_but_not_a_stricter_one()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");
        var b = await h.CreateSpecialtyAsync("Spec B");
        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), Classify(a), Classify(b));
        (await h.GetDocumentAsync(docId)).GetProperty("verificationStatus").GetString().Should().Be("Verified");
        (await h.GetClassificationDetailAsync(docId)).GetProperty("specialtyId").GetGuid().Should().Be(a);

        await h.UpdateSettingsAsync(s => s["agreementPercent"] = 100);
        var strict = await h.UploadUnclassifiedAsync();
        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), Classify(a), Classify(b));
        var summary = (await h.GetClassificationDetailAsync(strict)).GetProperty("summary");
        summary.GetProperty("verification").GetString().Should().Be("NeedsReview");
        summary.GetProperty("reviewReason").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task Three_way_disagreement_goes_to_the_conflicting_queue_and_is_not_verified()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");
        var b = await h.CreateSpecialtyAsync("Spec B");
        var c = await h.CreateSpecialtyAsync("Spec C");

        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), Classify(b), Classify(c));

        var doc = await h.GetDocumentAsync(docId);
        doc.GetProperty("verificationStatus").GetString().Should().Be("NeedsReview");
        doc.GetProperty("classificationStatus").GetString().Should().Be("Unclassified");

        var conflicting = Data(await h.Admin.GetStringAsync("/api/admin/classification/documents?queue=conflicting"));
        conflicting.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Contain(docId);
        var verified = Data(await h.Admin.GetStringAsync("/api/admin/classification/documents?queue=verified"));
        verified.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().NotContain(docId);
    }

    [Fact]
    public async Task Voters_who_leave_a_required_field_blank_do_not_make_up_a_consensus()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");

        // Only one of three supplied a specialty; type agrees. 1/3 < 66% → admin decides.
        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), new { decision = "Classify", documentType = "Exam" }, new { decision = "Classify", documentType = "Exam" });

        (await h.GetClassificationDetailAsync(docId)).GetProperty("summary").GetProperty("verification").GetString()
            .Should().Be("NeedsReview");
    }

    [Fact]
    public async Task Fewer_votes_than_required_leave_the_document_collecting_votes()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");
        await VoteAllAsync(await h.NewVotersAsync(2), Classify(a), Classify(a));

        var summary = (await h.GetClassificationDetailAsync(docId)).GetProperty("summary");
        summary.GetProperty("verification").GetString().Should().Be("Pending");
        summary.GetProperty("votes").GetInt32().Should().Be(2);
        var awaiting = Data(await h.Admin.GetStringAsync("/api/admin/classification/documents?queue=awaiting-votes"));
        awaiting.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Contain(docId);
    }

    [Fact]
    public async Task Non_educational_majority_goes_to_review_by_default_and_rejects_when_configured()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var review = await h.UploadUnclassifiedAsync();
        await VoteAllAsync(await h.NewVotersAsync(3), NotEducational, NotEducational, NotEducational);
        var summary = (await h.GetClassificationDetailAsync(review)).GetProperty("summary");
        summary.GetProperty("verification").GetString().Should().Be("NeedsReview");
        summary.GetProperty("reviewReason").GetString().Should().Be("non_educational");
        (await h.GetDocumentAsync(review)).GetProperty("status").GetString().Should().NotBe("Rejected");

        await h.UpdateSettingsAsync(s => s["nonEducationalPolicy"] = "AutoReject");
        var rejected = await h.UploadUnclassifiedAsync();
        await VoteAllAsync(await h.NewVotersAsync(3), NotEducational, NotEducational, NotEducational);
        var doc = await h.GetDocumentAsync(rejected);
        doc.GetProperty("status").GetString().Should().Be("Rejected");
        doc.GetProperty("verificationStatus").GetString().Should().Be("Rejected");
        doc.GetProperty("classificationStatus").GetString().Should().Be("NotEducational");
        Data(await h.Admin.GetStringAsync("/api/admin/classification/documents?queue=rejected"))
            .EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Contain(rejected);
    }

    [Fact]
    public async Task A_single_non_educational_vote_does_not_veto_an_otherwise_agreed_classification()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");

        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), Classify(a), NotEducational);

        (await h.GetDocumentAsync(docId)).GetProperty("verificationStatus").GetString().Should().Be("Verified");
    }

    // ------------------------------------------------------------------ admin overrides (spec §4)

    [Fact]
    public async Task Admin_can_verify_correct_reject_and_reopen_with_a_full_audit_trail()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var a = await h.CreateSpecialtyAsync("Spec A");
        var b = await h.CreateSpecialtyAsync("Spec B");
        var c = await h.CreateSpecialtyAsync("Spec C");
        await VoteAllAsync(await h.NewVotersAsync(3), Classify(a), Classify(b), Classify(c));

        // Correct → verified with the admin's own values (and a module so it can be published later).
        var verify = await h.Admin.PostAsJsonAsync($"/api/admin/classification/documents/{docId}/verify",
            new { specialtyId = b, documentType = "TD", moduleId = h.Tree.ModuleId, note = "Checked by hand" });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await h.GetDocumentAsync(docId);
        doc.GetProperty("verificationStatus").GetString().Should().Be("Verified");
        doc.GetProperty("moduleId").GetGuid().Should().Be(h.Tree.ModuleId);
        doc.GetProperty("type").GetString().Should().Be("TD");

        // Reopen → a fresh round: earlier votes remain as history, everyone may vote again.
        (await h.Admin.PostAsJsonAsync($"/api/admin/classification/documents/{docId}/reopen", new { note = "wrong" }))
            .EnsureSuccessStatusCode();
        var reopened = await h.GetClassificationDetailAsync(docId);
        reopened.GetProperty("summary").GetProperty("round").GetInt32().Should().Be(2);
        reopened.GetProperty("summary").GetProperty("votes").GetInt32().Should().Be(0);
        reopened.GetProperty("votes").GetArrayLength().Should().Be(3, "history is retained");
        var again = await h.NewVoterAsync();
        (await again.TaskAsync()).Should().ContainSingle(x => x.DocumentId == docId);

        // Reject.
        (await h.Admin.PostAsJsonAsync($"/api/admin/classification/documents/{docId}/reject", new { note = "scan of a menu" }))
            .EnsureSuccessStatusCode();
        (await h.GetDocumentAsync(docId)).GetProperty("status").GetString().Should().Be("Rejected");

        var actions = (await h.GetClassificationDetailAsync(docId)).GetProperty("history").EnumerateArray()
            .Select(e => e.GetProperty("action").GetString()).ToList();
        actions.Should().Contain(new[]
        {
            "classification.needs_review", "classification.admin_verified",
            "classification.admin_reopened", "classification.admin_rejected",
        });
    }

    [Fact]
    public async Task A_document_without_a_module_cannot_be_published_until_it_is_classified()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var publish = await h.Admin.PostAsJsonAsync($"/api/documents/{docId}/status", new { status = "Published" });
        publish.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unclassified_documents_are_not_visible_to_students_or_search()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var title = $"Secret-{Guid.NewGuid():N}";
        await h.UploadUnclassifiedAsync(title);
        var student = await h.NewVoterAsync();

        Data(await student.Client.GetStringAsync($"/api/search?q={title}")).GetArrayLength().Should().Be(0);
        var list = await student.Client.GetStringAsync("/api/documents");
        list.Should().NotContain(title);
    }

    private static JsonElement Data(string json) => ClassificationHarness.Data(json);
}
