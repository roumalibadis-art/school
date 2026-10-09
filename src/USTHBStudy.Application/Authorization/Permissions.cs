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

    public static class Classification
    {
        /// <summary>Review queues, verify / correct / reject / reopen classifications, classification reports.</summary>
        public const string Review = "Classification.Review";

        /// <summary>Edit the community-classification, consensus and reward configuration.</summary>
        public const string Settings = "Classification.Settings";
    }

    public static class Taxonomy
    {
        /// <summary>Approve, rename, merge or reject user-submitted taxonomy values.</summary>
        public const string Review = "Taxonomy.Review";
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
        yield return Classification.Review;
        yield return Classification.Settings;
        yield return Taxonomy.Review;
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
                Classification.Review, Taxonomy.Review,
            },
            [Roles.Student] = new[]
            {
                Documents.View,
            },
        };
}
