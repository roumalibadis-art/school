namespace USTHBStudy.Application.Documents;

using USTHBStudy.Application.Common;

public interface IDocumentService
{
    Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, DocumentFile file, CancellationToken ct = default);

    Task<PagedResult<DocumentDto>> ListAsync(DocumentQuery query, CancellationToken ct = default);

    Task<DocumentDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<DocumentDto> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<DocumentDto> UpdateAsync(Guid id, DocumentMetadataUpdate update, CancellationToken ct = default);

    Task<DocumentDto> ChangeStatusAsync(Guid id, DocumentStatusChange change, CancellationToken ct = default);

    /// <summary>Links a solution document to the exam/test/exercise it solves (PRD §13).</summary>
    Task LinkSolutionAsync(Guid parentDocumentId, Guid solutionDocumentId, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>The first-page preview image (PRD §31). Available for published documents.</summary>
    Task<DocumentContent> OpenPreviewAsync(string slug, CancellationToken ct = default);
}
