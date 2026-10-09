namespace USTHBStudy.Infrastructure.Classification;

using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Admin;
using USTHBStudy.Application.Classification;
using USTHBStudy.Domain.Classification;
using USTHBStudy.Infrastructure.Persistence;

public sealed class ClassificationSettingsService : IClassificationSettingsService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public ClassificationSettingsService(
        AppDbContext db, IDateTimeProvider clock, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ClassificationSettingsDto> GetAsync(CancellationToken ct = default) =>
        ClassificationSettingsStore.Map(await ClassificationSettingsStore.LoadAsync(_db, _clock, ct));

    public async Task<ClassificationSettingsDto> UpdateAsync(
        UpdateClassificationSettingsRequest r, CancellationToken ct = default)
    {
        var entity = await ClassificationSettingsStore.LoadAsync(_db, _clock, ct, track: true);
        var before = ClassificationSettingsStore.Map(entity);

        entity.DocumentsPerTask = r.DocumentsPerTask;
        entity.AssignmentExpiryHours = r.AssignmentExpiryHours;
        entity.MinSecondsBeforeVote = r.MinSecondsBeforeVote;
        entity.RequiredVoters = r.RequiredVoters;
        entity.AgreementPercent = r.AgreementPercent;
        entity.RequiredFields = ClassificationSettingsStore.ParseFields(r.RequiredFields);
        entity.NonEducationalPercent = r.NonEducationalPercent;
        entity.NonEducationalPolicy = Enum.Parse<NonEducationalPolicy>(r.NonEducationalPolicy, true);
        entity.LoginTriggerEnabled = r.LoginTriggerEnabled;
        entity.DownloadTriggerEnabled = r.DownloadTriggerEnabled;
        entity.DownloadsPerPrompt = r.DownloadsPerPrompt;
        entity.PromptSnoozeMinutes = r.PromptSnoozeMinutes;
        entity.QuotaEnabled = r.QuotaEnabled;
        entity.FreeDownloadsPerWindow = r.FreeDownloadsPerWindow;
        entity.QuotaWindowDays = r.QuotaWindowDays;
        entity.BonusDownloadsPerContribution = r.BonusDownloadsPerContribution;
        entity.MaxBonusPerWindow = r.MaxBonusPerWindow;
        entity.MaxRewardedContributionsPerDay = r.MaxRewardedContributionsPerDay;
        entity.MaxPendingProposalsPerUser = r.MaxPendingProposalsPerUser;
        entity.UpdatedAt = _clock.UtcNow;
        entity.UpdatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        var after = ClassificationSettingsStore.Map(entity);
        await _audit.WriteAsync("classification.settings_updated", "ClassificationSettings", entity.Id.ToString(),
            new { before, after }, ct);
        return after;
    }
}
