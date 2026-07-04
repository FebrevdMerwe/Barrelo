using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Entities;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.RestoreSessionPlayer;

public sealed class RestoreSessionPlayerCommandHandler(ISessionPlayerStore sessionPlayerStore)
    : IRequestHandler<RestoreSessionPlayerCommand, ErrorOr<Success>>
{
    public Task<ErrorOr<Success>> Handle(RestoreSessionPlayerCommand request, CancellationToken ct)
    {
        var player = Player.Restore(request.Id, request.Name, request.CreatedAtUtc);
        sessionPlayerStore.AddSessionPlayer(player);

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
