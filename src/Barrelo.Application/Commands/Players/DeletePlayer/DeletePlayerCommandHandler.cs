using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Errors;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.DeletePlayer;

/// <summary>Permanently removes a player from the roster (Manage Roster's hard delete) — unconditional,
/// the "are you sure" gate is a client-side confirm dialog only.</summary>
public sealed class DeletePlayerCommandHandler(
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePlayerCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeletePlayerCommand request, CancellationToken ct)
    {
        var player = await playerRepository.GetById(request.PlayerId, ct);
        if (player is null)
            return PlayerErrors.NotFound;

        playerRepository.Remove(player);
        await unitOfWork.SaveChangesAsync(ct);
        sessionPlayerStore.UnbenchPermanentPlayer(request.PlayerId);

        return Result.Deleted;
    }
}
