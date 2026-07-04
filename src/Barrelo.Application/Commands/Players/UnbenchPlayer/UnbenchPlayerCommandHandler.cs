using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Errors;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.UnbenchPlayer;

public sealed class UnbenchPlayerCommandHandler(
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore)
    : IRequestHandler<UnbenchPlayerCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UnbenchPlayerCommand request, CancellationToken ct)
    {
        var permanentPlayer = await playerRepository.GetById(request.PlayerId, ct);
        if (permanentPlayer is null)
            return PlayerErrors.NotFound;

        sessionPlayerStore.UnbenchPermanentPlayer(request.PlayerId);
        return Result.Success;
    }
}
