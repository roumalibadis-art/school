# Community classification

Students help classify unclassified documents; agreement between independent voters — not a single click and
not a bare vote count — verifies the result. Everything below is configurable from
**Admin → Paramètres contribution** (`/admin/classification/settings`, permission `Classification.Settings`).

## Concepts

| Concept | Meaning |
|---|---|
| **ClassificationStatus** | Does the document carry usable academic metadata? `Unclassified` · `Classified` · `NotEducational` |
| **VerificationStatus** | How far through community verification? `Unverified` (legacy / staff-classified, never in the process) · `Pending` (collecting votes) · `NeedsReview` (an admin must decide) · `Verified` · `Rejected` |
| **Round** | `Document.VotingRound`. An admin reopening a document starts a new round; earlier votes stay as history but stop counting. |
| **Task** | A batch of documents (default **3**) handed to one user. Open tasks are resumed, never duplicated. Expire after `AssignmentExpiryHours`. |
| **Assignment** | One document → one user → one round. Unique `(document, user, round)`. |
| **Vote** | One user's verdict for one document and round. Unique `(document, user, round)` — enforced by the database. |

A document enters the queue when it is `Unclassified` + `Pending`: today that means it was uploaded
**without a module** (admin upload; `ModuleId` is now nullable). Existing documents are backfilled as
`Classified` / `Unverified` by the migration, so they never appear in the queue.

## Who is asked, and who is not

A document is offered to a user only if: it has a free slot in the current round
(`votes + live assignments < RequiredVoters`, default **3**), the user never held it in this round (answered,
skipped, expired), and the user did not upload it. Documents closest to quorum come first. Suspended users
are refused at the action (their JWT may still be valid). University, role and profile are **not** criteria.

## Consensus policy (`ConsensusEvaluator`, pure and unit-tested)

Evaluated once at least `RequiredVoters` votes exist for the round.

1. **Non-educational** — if `NotEducational` votes ≥ `NonEducationalPercent` (default 66 %) of all votes, apply
   `NonEducationalPolicy`: `SendToReview` (default → `NeedsReview`, reason `non_educational`) or `AutoReject`
   (document `Rejected`). A single dissent never vetoes an otherwise agreed classification.
2. **Required fields** (default Specialty + DocumentType). Each must reach `AgreementPercent` (default 66 %) of
   **all** votes — abstaining counts against — with a *unique* leading value (a tie never verifies) and the value
   must be an approved one (an agreed *pending proposal* → `NeedsReview`, reason `pending_taxonomy`).
3. **Optional fields** (Department, AcademicYear, Session) apply only if ≥ 2 voters supplied them and they agree;
   otherwise they stay empty and do not block.
4. Failing a required field → `NeedsReview` with reason `conflict` (several competing values) or
   `insufficient_agreement`. Nothing is auto-published: verification fills `Type`, year, session, specialty and
   department (department derived from the specialty); **publishing still requires a module** and the existing
   `Document.Publish` permission.

Admins can **verify/correct** (explicit values, or the leading votes), **reject** or **reopen** from
`/admin/classification/{id}`. Every outcome and override is written to the audit log
(`classification.verified|needs_review|rejected|admin_verified|admin_rejected|admin_reopened`).

## Triggers and rewards

* **Prompt** (`GET /api/classification/prompt`, read-only): fires on a **login** (a login after the last prompt)
  and/or after **N downloads** (default 10) — each trigger independently switchable. One open task ⇒ never a
  second prompt. Answering (`start` / `later`) *consumes* both counters, so triggers can never stack up.
  `later` snoozes (`PromptSnoozeMinutes`). The UI never prompts on login/register, profile (onboarding), payment,
  upload, `/classify` or the admin console.
* **Valid contribution** = a recorded, non-duplicate vote (any decision) within the daily cap
  (`MaxRewardedContributionsPerDay`). Duplicates, repeats and answers faster than `MinSecondsBeforeVote` earn
  nothing. **Skipping** earns nothing and costs nothing, frees the slot for others and is not re-offered.
* **Free-tier download quota** (`QuotaEnabled`, **off by default** — nothing changes until switched on):
  `FreeDownloadsPerWindow` per `QuotaWindowDays`, plus `BonusDownloadsPerContribution` per valid contribution up
  to `MaxBonusPerWindow`. Spending is one atomic SQL statement, so parallel requests cannot overspend. The window
  resets bonus and usage together. Exhausted ⇒ HTTP 403 with error code `contribution_required`; the UI explains
  it and links to `/classify`.
* **Never bypassed:** the quota only *adds* a restriction on free users' free documents. Premium content still
  needs an active subscription, Premium members and document staff are exempt, suspended users are refused, and
  anonymous download stays 401. Reading a document in the in-browser reader counts as a download.

## User-proposed taxonomy ("Add new…")

`POST /api/classification/proposals` normalises (whitespace, NFC, no control/markup characters, 2–120 chars;
academic years must look like `2024-2025`), then:

1. exact match with an existing value (accent/case-insensitive; document-type aliases like *examen*) → `ExistingValue`;
2. identical proposal already pending → `ExistingProposal` (shared, so independent voters can agree; the other
   submitter is not revealed);
3. otherwise a `Pending` proposal is created (cap `MaxPendingProposalsPerUser`). Look-alikes are returned and
   the UI asks "Vouliez-vous dire…?" before submitting.

Pending values are **not** specialties/departments/years/sessions: they appear in no public list or search, and
only their submitter sees them in their own options. Admin (`/admin/taxonomy`, `Taxonomy.Review`): **approve**
(creates the real entity with a unique slug; specialty needs a department, department a faculty), **rename**,
**merge** into an existing value (votes are re-pointed) or **reject** (votes lose that field). Each decision
re-evaluates the affected documents. Document types are a fixed enum: they can be merged or rejected, not
approved as new.

## Concurrency

All voting state changes take the document's row first through an atomic compare-and-bump of
`Document.ClassificationVersion` (`DocumentLock`), inside one transaction; losers of an optimistic race or of a
MySQL deadlock (1213/1205) are retried; unique indexes turn duplicate votes/assignments/proposals into clean 409s.
Verified on real MySQL by `MySqlConcurrencyTests`.
