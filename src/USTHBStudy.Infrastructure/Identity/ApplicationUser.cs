namespace USTHBStudy.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using USTHBStudy.Domain.Common;

/// <summary>
/// Application user (PRD §9). Extends ASP.NET Core Identity with name, an optional student id,
/// the academic-profile foreign keys (wired to academic entities in Phase 2), Premium state and
/// an active/suspended flag. Create/update timestamps are set by the auditing interceptor.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Optional — not required unless a real business need exists (PRD §9).</summary>
    public string? StudentId { get; set; }

    public Guid? UniversityId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? SpecialtyId { get; set; }
    public Guid? LevelId { get; set; }

    /// <summary>Convenience flag; the source of truth is an active <c>Subscription</c> (Phase 6).</summary>
    public bool IsPremium { get; set; }

    /// <summary>Server-enforced Premium expiry (PRD §24).</summary>
    public DateTime? PremiumExpiresAt { get; set; }

    /// <summary>Suspended users fail authorization regardless of role (PRD §33/§61).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
