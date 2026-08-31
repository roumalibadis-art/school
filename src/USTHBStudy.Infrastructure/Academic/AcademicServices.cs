namespace USTHBStudy.Infrastructure.Academic;

using Microsoft.EntityFrameworkCore;
using USTHBStudy.Application.Academic;
using USTHBStudy.Domain.Academic;
using USTHBStudy.Infrastructure.Persistence;

public sealed class UniversityService : AcademicNodeService<University, UniversityDto, UniversityInput>, IUniversityService
{
    public UniversityService(AcademicServiceDependencies deps) : base(deps) { }

    protected override UniversityDto Map(University e) =>
        new(e.Id, e.Name, e.Slug, e.Code, e.City, e.Country, e.IsActive);

    protected override async Task<University> BuildAsync(UniversityInput input, CancellationToken ct)
    {
        var e = new University();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(UniversityInput input, University e, CancellationToken ct)
    {
        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.City = input.City?.Trim();
        e.Country = string.IsNullOrWhiteSpace(input.Country) ? "Algeria" : input.Country.Trim();
        e.IsActive = input.IsActive;
    }
}

public sealed class FacultyService : AcademicNodeService<Faculty, FacultyDto, FacultyInput>, IFacultyService
{
    public FacultyService(AcademicServiceDependencies deps) : base(deps) { }

    protected override FacultyDto Map(Faculty e) => new(e.Id, e.Name, e.Slug, e.Code, e.UniversityId, e.IsActive);

    protected override IQueryable<Faculty> ApplyParentFilter(IQueryable<Faculty> q, Guid parentId) =>
        q.Where(f => f.UniversityId == parentId);

    protected override async Task<Faculty> BuildAsync(FacultyInput input, CancellationToken ct)
    {
        var e = new Faculty();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(FacultyInput input, Faculty e, CancellationToken ct)
    {
        await RequireExistsAsync<University>(input.UniversityId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.UniversityId = input.UniversityId;
        e.IsActive = input.IsActive;
    }
}

public sealed class DepartmentService : AcademicNodeService<Department, DepartmentDto, DepartmentInput>, IDepartmentService
{
    public DepartmentService(AcademicServiceDependencies deps) : base(deps) { }

    protected override DepartmentDto Map(Department e) => new(e.Id, e.Name, e.Slug, e.Code, e.FacultyId, e.IsActive);

    protected override IQueryable<Department> ApplyParentFilter(IQueryable<Department> q, Guid parentId) =>
        q.Where(d => d.FacultyId == parentId);

    protected override async Task<Department> BuildAsync(DepartmentInput input, CancellationToken ct)
    {
        var e = new Department();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(DepartmentInput input, Department e, CancellationToken ct)
    {
        await RequireExistsAsync<Faculty>(input.FacultyId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.FacultyId = input.FacultyId;
        e.IsActive = input.IsActive;
    }
}

public sealed class DomainService : AcademicNodeService<AcademicDomain, DomainDto, DomainInput>, IDomainService
{
    public DomainService(AcademicServiceDependencies deps) : base(deps) { }

    protected override DomainDto Map(AcademicDomain e) => new(e.Id, e.Name, e.Slug, e.Code, e.FacultyId, e.IsActive);

    protected override IQueryable<AcademicDomain> ApplyParentFilter(IQueryable<AcademicDomain> q, Guid parentId) =>
        q.Where(d => d.FacultyId == parentId);

    protected override async Task<AcademicDomain> BuildAsync(DomainInput input, CancellationToken ct)
    {
        var e = new AcademicDomain();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(DomainInput input, AcademicDomain e, CancellationToken ct)
    {
        await RequireExistsAsync<Faculty>(input.FacultyId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.FacultyId = input.FacultyId;
        e.IsActive = input.IsActive;
    }
}

public sealed class SpecialtyService : AcademicNodeService<Specialty, SpecialtyDto, SpecialtyInput>, ISpecialtyService
{
    public SpecialtyService(AcademicServiceDependencies deps) : base(deps) { }

    protected override SpecialtyDto Map(Specialty e) =>
        new(e.Id, e.Name, e.Slug, e.Code, e.Description, e.DepartmentId, e.AcademicDomainId, e.IsActive);

    protected override IQueryable<Specialty> ApplyParentFilter(IQueryable<Specialty> q, Guid parentId) =>
        q.Where(s => s.DepartmentId == parentId);

    protected override async Task<Specialty> BuildAsync(SpecialtyInput input, CancellationToken ct)
    {
        var e = new Specialty();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(SpecialtyInput input, Specialty e, CancellationToken ct)
    {
        await RequireExistsAsync<Department>(input.DepartmentId, ct);
        if (input.DomainId is { } domainId)
        {
            await RequireExistsAsync<AcademicDomain>(domainId, ct);
        }

        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.Description = input.Description?.Trim();
        e.DepartmentId = input.DepartmentId;
        e.AcademicDomainId = input.DomainId;
        e.IsActive = input.IsActive;
    }
}

public sealed class LevelService : AcademicNodeService<Level, LevelDto, LevelInput>, ILevelService
{
    public LevelService(AcademicServiceDependencies deps) : base(deps) { }

    protected override LevelDto Map(Level e) =>
        new(e.Id, e.Name, e.Slug, e.ShortName, e.Cycle.ToString(), e.Order, e.SpecialtyId, e.IsActive);

    protected override IQueryable<Level> ApplyParentFilter(IQueryable<Level> q, Guid parentId) =>
        q.Where(l => l.SpecialtyId == parentId);

    protected override IQueryable<Level> DefaultOrder(IQueryable<Level> q) => q.OrderBy(l => l.Order).ThenBy(l => l.Name);

    protected override async Task<Level> BuildAsync(LevelInput input, CancellationToken ct)
    {
        var e = new Level();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(LevelInput input, Level e, CancellationToken ct)
    {
        await RequireExistsAsync<Specialty>(input.SpecialtyId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.ShortName = input.ShortName.Trim();
        e.Cycle = Enum.Parse<StudyCycle>(input.Cycle, ignoreCase: true);
        e.Order = input.Order;
        e.SpecialtyId = input.SpecialtyId;
        e.IsActive = input.IsActive;
    }
}

public sealed class SemesterService : AcademicNodeService<Semester, SemesterDto, SemesterInput>, ISemesterService
{
    public SemesterService(AcademicServiceDependencies deps) : base(deps) { }

    protected override SemesterDto Map(Semester e) =>
        new(e.Id, e.Name, e.Slug, e.ShortName, e.Order, e.LevelId, e.IsActive);

    protected override IQueryable<Semester> ApplyParentFilter(IQueryable<Semester> q, Guid parentId) =>
        q.Where(s => s.LevelId == parentId);

    protected override IQueryable<Semester> DefaultOrder(IQueryable<Semester> q) => q.OrderBy(s => s.Order).ThenBy(s => s.Name);

    protected override async Task<Semester> BuildAsync(SemesterInput input, CancellationToken ct)
    {
        var e = new Semester();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(SemesterInput input, Semester e, CancellationToken ct)
    {
        await RequireExistsAsync<Level>(input.LevelId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.ShortName = input.ShortName.Trim();
        e.Order = input.Order;
        e.LevelId = input.LevelId;
        e.IsActive = input.IsActive;
    }
}

public sealed class AcademicYearService : AcademicNodeService<AcademicYear, AcademicYearDto, AcademicYearInput>, IAcademicYearService
{
    public AcademicYearService(AcademicServiceDependencies deps) : base(deps) { }

    protected override AcademicYearDto Map(AcademicYear e) =>
        new(e.Id, e.Name, e.Slug, e.StartYear, e.EndYear, e.IsCurrent, e.IsActive);

    protected override IQueryable<AcademicYear> DefaultOrder(IQueryable<AcademicYear> q) => q.OrderByDescending(y => y.StartYear);

    protected override async Task<AcademicYear> BuildAsync(AcademicYearInput input, CancellationToken ct)
    {
        var e = new AcademicYear();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(AcademicYearInput input, AcademicYear e, CancellationToken ct)
    {
        e.StartYear = input.StartYear;
        e.EndYear = input.StartYear + 1;
        await SetNameAsync(e, $"{e.StartYear}-{e.EndYear}", ct);
        e.IsActive = input.IsActive;

        if (input.IsCurrent && !e.IsCurrent)
        {
            await Db.AcademicYears.Where(y => y.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false), ct);
        }

        e.IsCurrent = input.IsCurrent;
    }
}

public sealed class SessionService : AcademicNodeService<Session, SessionDto, SessionInput>, ISessionService
{
    public SessionService(AcademicServiceDependencies deps) : base(deps) { }

    protected override SessionDto Map(Session e) => new(e.Id, e.Name, e.Slug, e.Kind.ToString(), e.Order, e.IsActive);

    protected override IQueryable<Session> DefaultOrder(IQueryable<Session> q) => q.OrderBy(s => s.Order).ThenBy(s => s.Name);

    protected override async Task<Session> BuildAsync(SessionInput input, CancellationToken ct)
    {
        var e = new Session();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(SessionInput input, Session e, CancellationToken ct)
    {
        await SetNameAsync(e, input.Name, ct);
        e.Kind = Enum.Parse<SessionKind>(input.Kind, ignoreCase: true);
        e.Order = input.Order;
        e.IsActive = input.IsActive;
    }
}

public sealed class ModuleService : AcademicNodeService<Module, ModuleDto, ModuleInput>, IModuleService
{
    public ModuleService(AcademicServiceDependencies deps) : base(deps) { }

    protected override ModuleDto Map(Module e) => new(
        e.Id, e.Name, e.Slug, e.Code, e.Description, e.Coefficient, e.Credits, e.SemesterId, e.SpecialtyId, e.IsActive);

    protected override IQueryable<Module> ApplyParentFilter(IQueryable<Module> q, Guid parentId) =>
        q.Where(m => m.SemesterId == parentId || m.SpecialtyId == parentId);

    protected override async Task<Module> BuildAsync(ModuleInput input, CancellationToken ct)
    {
        var e = new Module();
        await ApplyAsync(input, e, ct);
        return e;
    }

    protected override async Task ApplyAsync(ModuleInput input, Module e, CancellationToken ct)
    {
        await RequireExistsAsync<Semester>(input.SemesterId, ct);
        await RequireExistsAsync<Specialty>(input.SpecialtyId, ct);
        await SetNameAsync(e, input.Name, ct);
        e.Code = input.Code?.Trim();
        e.Description = input.Description?.Trim();
        e.Coefficient = input.Coefficient;
        e.Credits = input.Credits;
        e.SemesterId = input.SemesterId;
        e.SpecialtyId = input.SpecialtyId;
        e.IsActive = input.IsActive;
    }
}
