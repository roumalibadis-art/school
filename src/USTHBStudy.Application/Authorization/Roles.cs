namespace USTHBStudy.Application.Authorization;

/// <summary>Canonical role names (PRD §43). Seeded by the database seeder.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Moderator, Student };
}
