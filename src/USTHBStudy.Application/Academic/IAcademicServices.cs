namespace USTHBStudy.Application.Academic;

using USTHBStudy.Application.Common;

/// <summary>CRUD contract shared by every academic-hierarchy node (PRD §34).</summary>
public interface IAcademicNodeService<TDto, in TInput>
{
    Task<PagedResult<TDto>> ListAsync(AcademicQuery query, CancellationToken ct = default);

    Task<TDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<TDto> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<TDto> CreateAsync(TInput input, CancellationToken ct = default);

    Task<TDto> UpdateAsync(Guid id, TInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IUniversityService : IAcademicNodeService<UniversityDto, UniversityInput>;

public interface IFacultyService : IAcademicNodeService<FacultyDto, FacultyInput>;

public interface IDepartmentService : IAcademicNodeService<DepartmentDto, DepartmentInput>;

public interface IDomainService : IAcademicNodeService<DomainDto, DomainInput>;

public interface ISpecialtyService : IAcademicNodeService<SpecialtyDto, SpecialtyInput>;

public interface ILevelService : IAcademicNodeService<LevelDto, LevelInput>;

public interface ISemesterService : IAcademicNodeService<SemesterDto, SemesterInput>;

public interface IAcademicYearService : IAcademicNodeService<AcademicYearDto, AcademicYearInput>;

public interface ISessionService : IAcademicNodeService<SessionDto, SessionInput>;

public interface IModuleService : IAcademicNodeService<ModuleDto, ModuleInput>;
