using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Services;
using Darts.Domain.Entities;
using ErrorOr;

namespace Darts.Application.Commands.Players.CreateSessionPlayer;

/// <summary>The "chalk a name" flow — creates a session-scoped player that is never persisted to the database.</summary>
public sealed class CreateSessionPlayerCommandHandler(ISessionPlayerStore sessionPlayerStore)
    : IRequestHandler<CreateSessionPlayerCommand, ErrorOr<Player>>
{
    public Task<ErrorOr<Player>> Handle(CreateSessionPlayerCommand request, CancellationToken ct)
    {
        var playerResult = Player.Create(request.Name);
        if (playerResult.IsError)
            return Task.FromResult<ErrorOr<Player>>(playerResult.Errors);

        var player = playerResult.Value;
        sessionPlayerStore.AddSessionPlayer(player);

        return Task.FromResult<ErrorOr<Player>>(player);
    }
}
