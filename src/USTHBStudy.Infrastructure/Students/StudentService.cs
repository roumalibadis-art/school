namespace USTHBStudy.Infrastructure.Students;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Students;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Common;
using USTHBStudy.Domain.Documents;
using USTHBStudy.Infrastructure.Identity;
using USTHBStudy.Infrastructure.Persistence;

public sealed class StudentService : IStudentService
{
    private const int MyModulesLimit = 40;
    private const int RecentLimit = 6;
    private const int PopularExamsLimit = 5;

    private readonly AppDbContext _db;
    private readonly IIdentityService _identity;
    private readonly IAccessControlService _access;
    private readonly IDateTimeProvider _clock;

    public StudentService(
        AppDbContext db, IIdentityService identity, IAccessControlService access, IDateTimeProvider clock)
    {
        _db = db;
        _identity = identity;
        _access = access;
        _clock = clock;
    }

    public async Task<StudentProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);
        return await ToProfileAsync(user, ct);
    }

    public async Task<StudentProfileDto> UpdateProfileAsync(
        Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);

        await ValidateAsync<University>(request.UniversityId, ct);
        await ValidateAsync<Faculty>(request.FacultyId, ct);
        await ValidateAsync<Department>(request.DepartmentId, ct);
        await ValidateAsync<Specialty>(request.SpecialtyId, ct);
        await ValidateAsync<Level>(request.LevelId, ct);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.StudentId = string.IsNullOrWhiteSpace(request.StudentId) ? null : request.StudentId.Trim();
        user.UniversityId = request.UniversityId;
        user.FacultyId = request.FacultyId;
        user.DepartmentId = request.DepartmentId;
        user.SpecialtyId = request.SpecialtyId;
        user.LevelId = request.LevelId;

        await _db.SaveChangesAsync(ct);
        return await ToProfileAsync(user, ct);
    }

    public async Task<StudentDashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);

        var specialty = await RefAsync<Specialty>(user.SpecialtyId, ct);
        var level = await RefAsync<Level>(user.LevelId, ct);

        var myModules = await MyModulesAsync(user.SpecialtyId, user.LevelId, ct);
        var moduleIds = myModules.Select(m => m.Id).ToArray();

        var recent = user.SpecialtyId is { } specId
            ? await _db.Documents.AsNoTracking()
                .Where(d => d.Status == DocumentStatus.Published && d.Module!.SpecialtyId == specId)
                .OrderByDescending(d => d.PublishedAt ?? d.CreatedAt)
                .Take(RecentLimit)
                .Select(d => Summary(d))
                .ToListAsync(ct)
            : new List<DocumentSummary>();

        var popularExams = user.SpecialtyId is { } sid
            ? await _db.Documents.AsNoTracking()
                .Where(d => d.Status == DocumentStatus.Published
                            && d.Module!.SpecialtyId == sid
                            && (d.Type == DocumentType.Exam || d.Type == DocumentType.Test))
                .OrderByDescending(d => d.ViewCount)
                .Take(PopularExamsLimit)
                .Select(d => Summary(d))
                .ToListAsync(ct)
            : new List<DocumentSummary>();

        var favoritesCount = await _db.Favorites.CountAsync(f => f.UserId == userId, ct);

        return new StudentDashboardDto(
            user.FirstName,
            specialty,
            level,
            myModules,
            recent,
            popularExams,
            favoritesCount,
            SubscriptionStatus(user));
    }

    private async Task<IReadOnlyList<ModuleSummary>> MyModulesAsync(Guid? specialtyId, Guid? levelId, CancellationToken ct)
    {
        if (specialtyId is not { } specId)
        {
            return Array.Empty<ModuleSummary>();
        }

        var query = _db.Modules.AsNoTracking().Where(m => m.SpecialtyId == specId);
        if (levelId is { } lid)
        {
            query = query.Where(m => m.Semester!.LevelId == lid);
        }

        return await query
            .OrderBy(m => m.Semester!.Order).ThenBy(m => m.Name)
            .Take(MyModulesLimit)
            .Select(m => new ModuleSummary(m.Id, m.Name, m.Slug, m.Semester!.Name))
            .ToListAsync(ct);
    }

    private SubscriptionStatusDto SubscriptionStatus(ApplicationUser user)
    {
        var subject = new AccessSubject(user.IsActive, user.IsPremium, user.PremiumExpiresAt);
        if (_access.IsPremiumActive(subject))
        {
            return new SubscriptionStatusDto(true, user.PremiumExpiresAt, "active");
        }

        var expired = user.IsPremium && user.PremiumExpiresAt is { } exp && exp <= _clock.UtcNow;
        return new SubscriptionStatusDto(false, user.PremiumExpiresAt, expired ? "expired" : "none");
    }

    private async Task<StudentProfileDto> ToProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _identity.GetRolesAsync(user.Id, ct);
        var permissions = await _identity.GetPermissionsAsync(user.Id, ct);

        return new StudentProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.StudentId,
            user.IsActive,
            user.IsPremium,
            user.PremiumExpiresAt,
            await RefAsync<University>(user.UniversityId, ct),
            await RefAsync<Faculty>(user.FacultyId, ct),
            await RefAsync<Department>(user.DepartmentId, ct),
            await RefAsync<Specialty>(user.SpecialtyId, ct),
            await RefAsync<Level>(user.LevelId, ct),
            roles,
            permissions);
    }

    private async Task<AcademicRef?> RefAsync<T>(Guid? id, CancellationToken ct)
        where T : AcademicEntity
    {
        if (id is not { } value)
        {
            return null;
        }

        return await _db.Set<T>().AsNoTracking()
            .Where(e => e.Id == value)
            .Select(e => new AcademicRef(e.Id, e.Name, e.Slug))
            .FirstOrDefaultAsync(ct);
    }

    private async Task ValidateAsync<T>(Guid? id, CancellationToken ct)
        where T : AcademicEntity
    {
        if (id is { } value && !await _db.Set<T>().AnyAsync(e => e.Id == value, ct))
        {
            throw new NotFoundException(typeof(T).Name, value);
        }
    }

    private static DocumentSummary Summary(Document d) =>
        new(d.Id, d.Title, d.Slug, d.Type.ToString(), d.IsPremium, d.PreviewStorageKey != null, d.ViewCount);
}
