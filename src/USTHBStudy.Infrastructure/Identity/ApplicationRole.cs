namespace USTHBStudy.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

/// <summary>Role with an optional description. Permission grants are stored as role claims (PRD §43).</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }

    public string? Description { get; set; }
}
