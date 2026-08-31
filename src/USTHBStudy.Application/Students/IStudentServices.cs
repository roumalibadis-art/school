namespace USTHBStudy.Application.Students;

using USTHBStudy.Domain.Students;

public interface IStudentService
{
    Task<StudentProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);

    Task<StudentProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);

    Task<StudentDashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default);
}

public interface IFavoriteService
{
    Task<IReadOnlyList<FavoriteDto>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<FavoriteDto> AddAsync(Guid userId, FavoriteKind kind, Guid entityId, CancellationToken ct = default);

    Task RemoveAsync(Guid userId, FavoriteKind kind, Guid entityId, CancellationToken ct = default);
}

public interface IActivityService
{
    Task RecordDocumentViewAsync(Guid userId, Guid documentId, CancellationToken ct = default);

    Task RecordDocumentDownloadAsync(Guid userId, Guid documentId, CancellationToken ct = default);

    Task RecordModuleViewAsync(Guid userId, Guid moduleId, CancellationToken ct = default);

    Task<HistoryDto> GetHistoryAsync(Guid userId, CancellationToken ct = default);
}
