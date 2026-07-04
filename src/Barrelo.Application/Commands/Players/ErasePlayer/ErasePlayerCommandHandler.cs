using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Errors;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.ErasePlayer;

/// <summary>
/// Erasing a session-scoped player fully removes it from memory. Erasing a permanent player only
/// benches it for the rest of this session — the roster entry itself is untouched (see DeletePlayer
/// for the Manage Roster modal's actual hard delete).
/// </summary>
public sealed class ErasePlayerCommandHandler(
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore)
    : IRequestHandler<ErasePlayerCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(ErasePlayerCommand request, CancellationToken ct)
    {
        if (sessionPlayerStore.RemoveSessionPlayer(request.PlayerId))
            return Result.Deleted;

        var permanentPlayer = await playerRepository.GetById(request.PlayerId, ct);
        if (permanentPlayer is null)
            return PlayerErrors.NotFound;

        sessionPlayerStore.BenchPermanentPlayer(request.PlayerId);
        return Result.Deleted;
    }
}
