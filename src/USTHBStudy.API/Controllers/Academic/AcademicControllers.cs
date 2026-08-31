namespace USTHBStudy.API.Controllers.Academic;

using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Academic;

[Route("api/universities")]
public sealed class UniversitiesController : AcademicNodeController<UniversityDto, UniversityInput>
{
    public UniversitiesController(IUniversityService service) : base(service) { }
}

[Route("api/faculties")]
public sealed class FacultiesController : AcademicNodeController<FacultyDto, FacultyInput>
{
    public FacultiesController(IFacultyService service) : base(service) { }
}

[Route("api/departments")]
public sealed class DepartmentsController : AcademicNodeController<DepartmentDto, DepartmentInput>
{
    public DepartmentsController(IDepartmentService service) : base(service) { }
}

[Route("api/domains")]
public sealed class DomainsController : AcademicNodeController<DomainDto, DomainInput>
{
    public DomainsController(IDomainService service) : base(service) { }
}

[Route("api/specialties")]
public sealed class SpecialtiesController : AcademicNodeController<SpecialtyDto, SpecialtyInput>
{
    public SpecialtiesController(ISpecialtyService service) : base(service) { }
}

[Route("api/levels")]
public sealed class LevelsController : AcademicNodeController<LevelDto, LevelInput>
{
    public LevelsController(ILevelService service) : base(service) { }
}

[Route("api/semesters")]
public sealed class SemestersController : AcademicNodeController<SemesterDto, SemesterInput>
{
    public SemestersController(ISemesterService service) : base(service) { }
}

[Route("api/academic-years")]
public sealed class AcademicYearsController : AcademicNodeController<AcademicYearDto, AcademicYearInput>
{
    public AcademicYearsController(IAcademicYearService service) : base(service) { }
}

[Route("api/sessions")]
public sealed class SessionsController : AcademicNodeController<SessionDto, SessionInput>
{
    public SessionsController(ISessionService service) : base(service) { }
}

[Route("api/modules")]
public sealed class ModulesController : AcademicNodeController<ModuleDto, ModuleInput>
{
    public ModulesController(IModuleService service) : base(service) { }
}
