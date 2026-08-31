namespace USTHBStudy.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USTHBStudy.Application.Abstractions;
using USTHBStudy.Application.Common;
using USTHBStudy.Application.Students;
using USTHBStudy.Domain.Students;

[Authorize]
[Route("api/favorites")]
public sealed class FavoritesController : ApiControllerBase
{
    private readonly IFavoriteService _favorites;
    private readonly ICurrentUser _currentUser;

    public FavoritesController(IFavoriteService favorites, ICurrentUser currentUser)
    {
        _favorites = favorites;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAppException();

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FavoriteDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse.Data(await _favorites.ListAsync(UserId, ct)));

    /// <summary>Favorites a module or document (PRD §27). Idempotent — re-adding is a no-op.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<FavoriteDto>>> Add(AddFavoriteRequest request, CancellationToken ct)
    {
        var kind = Enum.Parse<FavoriteKind>(request.Kind, ignoreCase: true);
        return Ok(ApiResponse.Data(await _favorites.AddAsync(UserId, kind, request.EntityId, ct), "Added to favorites."));
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse>> Remove(
        [FromQuery] string kind, [FromQuery] Guid entityId, CancellationToken ct)
    {
        var parsed = Enum.Parse<FavoriteKind>(kind, ignoreCase: true);
        await _favorites.RemoveAsync(UserId, parsed, entityId, ct);
        return Ok(ApiResponse.Ok("Removed from favorites."));
    }
}
