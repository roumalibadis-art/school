namespace USTHBStudy.Domain.Documents;

/// <summary>Kind of academic resource (PRD §12). Stored as <c>int</c>.</summary>
public enum DocumentType
{
    Course = 1,
    TD = 2,
    TP = 3,
    Exam = 4,
    ExamSolution = 5,
    Test = 6,
    TestSolution = 7,
    Exercise = 8,
    ExerciseSolution = 9,
    Summary = 10,
    Other = 99,
}

/// <summary>Publication state (PRD §12). Only <see cref="Published"/> is visible to the public.</summary>
public enum DocumentStatus
{
    Draft = 1,
    PendingReview = 2,
    Published = 3,
    Rejected = 4,
    Archived = 5,
}

/// <summary>Content-rights classification (PRD §40).</summary>
public enum RightsStatus
{
    Unknown = 0,
    UserProvided = 1,
    Authorized = 2,
    PublicDomain = 3,
    Official = 4,
    Restricted = 5,
}
