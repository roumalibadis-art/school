namespace USTHBStudy.Application.Authorization;

/// <summary>
/// Explicit permission strings (PRD §43). Stored as <c>permission</c> claims on roles and
/// mapped 1:1 to ASP.NET Core authorization policies. Controllers reference these constants,
/// never role names, so the role→permission mapping can change without touching endpoints.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Documents
    {
        public const string View = "Document.View";
        public const string Create = "Document.Create";
        public const string Update = "Document.Update";
        public const string Delete = "Document.Delete";
        public const string Publish = "Document.Publish";
    }

    public static class Users
    {
        public const string View = "User.View";
        public const string Update = "User.Update";
        public const string Suspend = "User.Suspend";
    }

    public static class Subscriptions
    {
        public const string View = "Subscription.View";
        public const string Manage = "Subscription.Manage";
    }

    public static class Contributions
    {
        public const string Moderate = "Contribution.Moderate";
    }

    public static class AcademicData
    {
        public const string Manage = "AcademicData.Manage";
    }

    public static class Reports
    {
        public const string Resolve = "Report.Resolve";
    }

    public static class Audit
    {
        public const string View = "AuditLog.View";
    }

    /// <summary>Every permission string, for policy registration.</summary>
    public static IEnumerable<string> All()
    {
        yield return Documents.View;
        yield return Documents.Create;
        yield return Documents.Update;
        yield return Documents.Delete;
        yield return Documents.Publish;
        yield return Users.View;
        yield return Users.Update;
        yield return Users.Suspend;
        yield return Subscriptions.View;
        yield return Subscriptions.Manage;
        yield return Contributions.Moderate;
        yield return AcademicData.Manage;
        yield return Reports.Resolve;
        yield return Audit.View;
    }

    /// <summary>Default role → permission grants seeded on first run (PRD §43).</summary>
    public static IReadOnlyDictionary<string, string[]> DefaultRoleGrants { get; } =
        new Dictionary<string, string[]>
        {
            [Roles.Admin] = All().ToArray(),
            [Roles.Moderator] = new[]
            {
                Documents.View, Documents.Update, Documents.Publish,
                Contributions.Moderate, Reports.Resolve, Users.View,
            },
            [Roles.Student] = new[]
            {
                Documents.View,
            },
        };
}
