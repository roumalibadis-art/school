namespace USTHBStudy.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using USTHBStudy.IntegrationTests.TestSupport;

/// <summary>"Add new…" proposals and their moderation (spec tests 10–11).</summary>
public class TaxonomyProposalTests
{
    private static JsonElement Data(string json) => ClassificationHarness.Data(json);

    private static Task<HttpResponseMessage> Propose(Voter v, string category, string value, Guid? parentId = null, Guid? documentId = null) =>
        v.Client.PostAsJsonAsync("/api/classification/proposals", new { category, value, parentId, documentId });

    // ------------------------------------------------------------------ creation / validation (test 10)

    [Fact]
    public async Task A_proposal_is_validated_normalised_and_stays_pending_and_invisible()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();

        var response = await Propose(voter, "Specialty", "   Génie    Logiciel  ", h.Tree.DepartmentId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await ClassificationHarness.DataAsync(response);
        data.GetProperty("outcome").GetString().Should().Be("Created");
        var proposal = data.GetProperty("proposal");
        proposal.GetProperty("value").GetString().Should().Be("Génie Logiciel", "whitespace is collapsed");
        proposal.GetProperty("status").GetString().Should().Be("Pending");
        proposal.GetProperty("submittedById").GetGuid().Should().Be(voter.Id);
        proposal.GetProperty("submittedAt").ValueKind.Should().Be(JsonValueKind.String);

        // Not a real specialty: absent from the public taxonomy lists, search and everyone else's options.
        (await h.Admin.GetStringAsync("/api/specialties?pageSize=100")).Should().NotContain("Génie Logiciel");
        var other = await h.NewVoterAsync();
        var otherOptions = Data(await other.Client.GetStringAsync("/api/classification/options"));
        otherOptions.GetProperty("myPendingProposals").GetArrayLength().Should().Be(0);
        otherOptions.GetProperty("specialties").EnumerateArray().Select(s => s.GetProperty("name").GetString())
            .Should().NotContain("Génie Logiciel");

        // The submitter sees their own pending value, flagged as pending, so they can keep classifying.
        var mine = Data(await voter.Client.GetStringAsync("/api/classification/options")).GetProperty("myPendingProposals");
        mine.EnumerateArray().Should().ContainSingle(p => p.GetProperty("category").GetString() == "Specialty"
                                                            && p.GetProperty("name").GetString() == "Génie Logiciel");
    }

    [Theory]
    [InlineData("Specialty", "a")]
    [InlineData("Specialty", "<script>alert(1)</script>")]
    [InlineData("Specialty", "   ")]
    [InlineData("Department", "{{template}}")]
    [InlineData("AcademicYear", "not a year")]
    [InlineData("AcademicYear", "2024-2030")]
    [InlineData("Nonsense", "Whatever")]
    public async Task Invalid_proposals_are_rejected(string category, string value)
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        (await Propose(voter, category, value)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Proposing_an_existing_value_or_an_alias_points_at_the_existing_one()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var existing = await h.CreateSpecialtyAsync("Informatique Générale");
        var voter = await h.NewVoterAsync();

        var exact = await ClassificationHarness.DataAsync(await Propose(voter, "Specialty", "informatique  generale", h.Tree.DepartmentId));
        exact.GetProperty("outcome").GetString().Should().Be("ExistingValue");
        exact.GetProperty("existing").GetProperty("id").GetGuid().Should().Be(existing);

        var type = await ClassificationHarness.DataAsync(await Propose(voter, "DocumentType", "Examen"));
        type.GetProperty("outcome").GetString().Should().Be("ExistingValue");
        type.GetProperty("existing").GetProperty("enumValue").GetString().Should().Be("Exam");

        var year = await h.CreateAcademicYearAsync(2023);
        var yearDup = await ClassificationHarness.DataAsync(await Propose(voter, "AcademicYear", "2023/2024"));
        yearDup.GetProperty("outcome").GetString().Should().Be("ExistingValue");
        yearDup.GetProperty("existing").GetProperty("id").GetGuid().Should().Be(year);

        (await h.Admin.GetStringAsync("/api/admin/taxonomy/proposals?status=All")).Should().NotContain("Informatique");
    }

    [Fact]
    public async Task Near_duplicates_are_reported_as_similar_but_do_not_block_the_proposal()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.CreateSpecialtyAsync("Intelligence Artificielle");
        var voter = await h.NewVoterAsync();

        var data = await ClassificationHarness.DataAsync(await Propose(voter, "Specialty", "Intelligence Artificiele", h.Tree.DepartmentId));
        data.GetProperty("outcome").GetString().Should().Be("Created");
        data.GetProperty("similar").EnumerateArray().Select(s => s.GetProperty("name").GetString())
            .Should().Contain("Intelligence Artificielle");

        var live = Data(await voter.Client.GetStringAsync(
            "/api/classification/proposals/similar?category=Specialty&value=Intelligence%20Artificielle"));
        live.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_same_value_proposed_twice_shares_one_proposal_and_the_pending_cap_is_enforced()
    {
        await using var h = await ClassificationHarness.CreateAsync(s => s["maxPendingProposalsPerUser"] = 2);
        var one = await h.NewVoterAsync();
        var two = await h.NewVoterAsync();

        var first = await ClassificationHarness.DataAsync(await Propose(one, "Session", "Rattrapage spécial"));
        var second = await ClassificationHarness.DataAsync(await Propose(two, "Session", "rattrapage SPECIAL"));
        second.GetProperty("outcome").GetString().Should().Be("ExistingProposal");
        second.GetProperty("proposal").GetProperty("submittedByEmail").ValueKind.Should().Be(JsonValueKind.Null,
            "another student's identity must not leak through a shared proposal");
        second.GetProperty("proposal").GetProperty("submittedById").GetGuid().Should().Be(Guid.Empty);
        second.GetProperty("proposal").GetProperty("id").GetGuid().Should().Be(first.GetProperty("proposal").GetProperty("id").GetGuid());

        (await Propose(one, "Session", "Autre session A")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Propose(one, "Session", "Autre session B")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "cap of 2 pending");
    }

    [Fact]
    public async Task Suspended_users_cannot_propose_or_classify()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        await h.UploadUnclassifiedAsync();
        var voter = await h.NewVoterAsync();
        (await voter.TaskAsync()).Should().NotBeEmpty();

        (await h.Admin.PostAsJsonAsync($"/api/admin/users/{voter.Id}/suspend", new { reason = "abuse" })).EnsureSuccessStatusCode();

        // The old access token is still cryptographically valid — the action itself must refuse.
        (await Propose(voter, "Session", "Anything goes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await voter.Client.PostAsync("/api/classification/tasks/next", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ moderation (test 11)

    [Fact]
    public async Task Admin_can_approve_a_specialty_proposal_which_then_becomes_a_real_searchable_value()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "Specialty", "Cybersécurité", h.Tree.DepartmentId)))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        var pending = Data(await h.Admin.GetStringAsync("/api/admin/taxonomy/proposals?status=Pending&category=Specialty"));
        var row = pending.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == id);
        row.GetProperty("submittedByEmail").GetString().Should().Be(voter.Email);

        var approve = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { name = "Cyber-sécurité", note = "ok" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await ClassificationHarness.DataAsync(approve);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("approvedName").GetString().Should().Be("Cyber-sécurité");
        approved.GetProperty("reviewedAt").ValueKind.Should().Be(JsonValueKind.String);
        var newId = approved.GetProperty("resolvedEntityId").GetGuid();

        var specialties = Data(await h.Admin.GetStringAsync($"/api/specialties?parentId={h.Tree.DepartmentId}&pageSize=100"));
        specialties.EnumerateArray().Should().Contain(s => s.GetProperty("id").GetGuid() == newId
                                                          && s.GetProperty("name").GetString() == "Cyber-sécurité");

        // Approving twice is refused.
        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Specialty_and_department_proposals_need_a_parent_before_approval()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "Specialty", "Sans parent")))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { parentId = h.Tree.DepartmentId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_can_approve_academic_year_and_session_proposals()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var year = (await ClassificationHarness.DataAsync(await Propose(voter, "AcademicYear", "2031 - 2032"))).GetProperty("proposal").GetProperty("id").GetGuid();
        var session = (await ClassificationHarness.DataAsync(await Propose(voter, "Session", "Examen de remplacement"))).GetProperty("proposal").GetProperty("id").GetGuid();

        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{year}/approve", new { })).EnsureSuccessStatusCode();
        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{session}/approve", new { })).EnsureSuccessStatusCode();

        (await h.Admin.GetStringAsync("/api/academic-years?pageSize=100")).Should().Contain("2031-2032");
        (await h.Admin.GetStringAsync("/api/sessions?pageSize=100")).Should().Contain("Examen de remplacement");
    }

    [Fact]
    public async Task Admin_can_rename_then_reject_and_rejected_values_cannot_be_re_proposed()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "Session", "sesion rate")))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        var rename = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/rename", new { name = "Session de rattrapage" });
        (await ClassificationHarness.DataAsync(rename)).GetProperty("approvedName").GetString().Should().Be("Session de rattrapage");

        var reject = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/reject", new { note = "duplicate of an existing concept" });
        var rejected = await ClassificationHarness.DataAsync(reject);
        rejected.GetProperty("status").GetString().Should().Be("Rejected");
        rejected.GetProperty("adminNote").GetString().Should().Contain("duplicate");

        (await Propose(voter, "Session", "sesion rate")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await h.Admin.GetStringAsync("/api/sessions?pageSize=100")).Should().NotContain("rattrapage");
    }

    [Fact]
    public async Task Admin_can_merge_a_duplicate_proposal_into_an_existing_value()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var target = await h.CreateSpecialtyAsync("Réseaux et Systèmes");
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "Specialty", "RSD", h.Tree.DepartmentId)))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        var merge = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/merge", new { targetId = target });
        merge.StatusCode.Should().Be(HttpStatusCode.OK);
        var merged = await ClassificationHarness.DataAsync(merge);
        merged.GetProperty("status").GetString().Should().Be("Merged");
        merged.GetProperty("resolvedEntityId").GetGuid().Should().Be(target);
        merged.GetProperty("resolvedName").GetString().Should().Be("Réseaux et Systèmes");

        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/merge", new { targetId = Guid.NewGuid() })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Document_type_proposals_can_be_merged_into_an_existing_type_but_not_approved_as_new()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "DocumentType", "Polycopié")))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/merge", new { targetDocumentType = "Nope" })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var merge = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/merge", new { targetDocumentType = "Course" });
        (await ClassificationHarness.DataAsync(merge)).GetProperty("resolvedDocumentType").GetString().Should().Be("Course");
    }

    // ------------------------------------------------------------------ interaction with voting

    [Fact]
    public async Task Votes_agreeing_on_a_pending_value_wait_for_the_admin_then_verify_automatically_on_approval()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var docId = await h.UploadUnclassifiedAsync();
        var voters = await h.NewVotersAsync(3);
        var proposalId = (await ClassificationHarness.DataAsync(
            await Propose(voters[0], "Specialty", "Robotique", h.Tree.DepartmentId, docId)))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        // The other two propose the identical value and therefore share the proposal.
        foreach (var v in voters.Skip(1))
        {
            (await ClassificationHarness.DataAsync(await Propose(v, "Specialty", "Robotique", h.Tree.DepartmentId)))
                .GetProperty("proposal").GetProperty("id").GetGuid().Should().Be(proposalId);
        }

        foreach (var v in voters)
        {
            var a = (await v.TaskAsync()).Single().AssignmentId;
            (await v.VoteAsync(a, new { decision = "Classify", specialtyProposalId = proposalId, documentType = "TP" })).EnsureSuccessStatusCode();
        }

        var summary = (await h.GetClassificationDetailAsync(docId)).GetProperty("summary");
        summary.GetProperty("verification").GetString().Should().Be("NeedsReview");
        summary.GetProperty("reviewReason").GetString().Should().Be("pending_taxonomy");

        // The reviewer can see which documents rely on the proposal.
        var detail = Data(await h.Admin.GetStringAsync($"/api/admin/taxonomy/proposals/{proposalId}"));
        detail.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Contain(docId);
        detail.GetProperty("proposal").GetProperty("voteCount").GetInt32().Should().Be(3);

        var approve = await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{proposalId}/approve", new { });
        var newId = (await ClassificationHarness.DataAsync(approve)).GetProperty("resolvedEntityId").GetGuid();

        var after = await h.GetClassificationDetailAsync(docId);
        after.GetProperty("summary").GetProperty("verification").GetString().Should().Be("Verified");
        after.GetProperty("specialtyId").GetGuid().Should().Be(newId);
        after.GetProperty("documentType").GetString().Should().Be("TP");
    }

    [Fact]
    public async Task Rejecting_a_proposal_strips_that_field_from_the_votes_and_merging_redirects_them()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var target = await h.CreateSpecialtyAsync("Mathématiques");
        var docId = await h.UploadUnclassifiedAsync();
        var voters = await h.NewVotersAsync(3);
        var proposalId = (await ClassificationHarness.DataAsync(await Propose(voters[0], "Specialty", "Maths", h.Tree.DepartmentId)))
            .GetProperty("proposal").GetProperty("id").GetGuid();
        await Propose(voters[1], "Specialty", "Maths", h.Tree.DepartmentId);
        await Propose(voters[2], "Specialty", "Maths", h.Tree.DepartmentId);

        foreach (var v in voters)
        {
            var a = (await v.TaskAsync()).Single().AssignmentId;
            (await v.VoteAsync(a, new { decision = "Classify", specialtyProposalId = proposalId, documentType = "Exam" })).EnsureSuccessStatusCode();
        }

        (await h.Admin.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{proposalId}/merge", new { targetId = target })).EnsureSuccessStatusCode();

        var detail = await h.GetClassificationDetailAsync(docId);
        detail.GetProperty("summary").GetProperty("verification").GetString().Should().Be("Verified");
        detail.GetProperty("specialtyId").GetGuid().Should().Be(target);
        detail.GetProperty("votes").EnumerateArray().Select(v => v.GetProperty("specialty").GetString())
            .Should().AllBe("Mathématiques");
    }

    // ------------------------------------------------------------------ authorization

    [Fact]
    public async Task Students_cannot_use_the_taxonomy_review_endpoints()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var voter = await h.NewVoterAsync();
        var id = (await ClassificationHarness.DataAsync(await Propose(voter, "Session", "Une session")))
            .GetProperty("proposal").GetProperty("id").GetGuid();

        (await voter.Client.GetAsync("/api/admin/taxonomy/proposals")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await voter.Client.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/approve", new { })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await voter.Client.PostAsJsonAsync($"/api/admin/taxonomy/proposals/{id}/merge", new { targetId = Guid.NewGuid() })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await h.Factory.CreateClient().GetAsync("/api/admin/taxonomy/proposals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Moderators_can_review_taxonomy_and_classification_but_not_change_settings()
    {
        await using var h = await ClassificationHarness.CreateAsync();
        var mod = await h.Factory.LoggedInClientAsync("moderator@example.local", "Moderator#2026!");

        (await mod.GetAsync("/api/admin/taxonomy/proposals")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.GetAsync("/api/admin/classification/documents")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.GetAsync("/api/admin/classification/report")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await mod.GetAsync("/api/admin/classification/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mod.PutAsJsonAsync("/api/admin/classification/settings", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
