namespace USTHBStudy.Domain.Students;

using USTHBStudy.Domain.Academic;
using USTHBStudy.Domain.Common;
using USTHBStudy.Domain.Documents;

public enum FavoriteKind
{
    Module = 1,
    Document = 2,
}

public enum UserActivityKind
{
    DocumentView = 1,
    DocumentDownload = 2,
    ModuleView = 3,
}

/// <summary>A student's saved item (PRD §27). Duplicates are prevented by a unique index.</summary>
public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }

    public FavoriteKind Kind { get; set; }

    /// <summary>Id of the favorited <see cref="Module"/> or <see cref="Document"/>.</summary>
    public Guid EntityId { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>A row in the student's activity history (PRD §28). Appended, then de-duplicated on read.</summary>
public class UserActivity : BaseEntity
{
    public Guid UserId { get; set; }

    public UserActivityKind Kind { get; set; }

    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid? ModuleId { get; set; }
    public Module? Module { get; set; }

    public DateTime OccurredAt { get; set; }
}
