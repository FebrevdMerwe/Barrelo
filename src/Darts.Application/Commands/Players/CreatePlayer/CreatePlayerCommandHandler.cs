using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Domain.Entities;
using ErrorOr;

namespace Darts.Application.Commands.Players.CreatePlayer;

public sealed class CreatePlayerCommandHandler(IPlayerRepository playerRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePlayerCommand, ErrorOr<Player>>
{
    public async Task<ErrorOr<Player>> Handle(CreatePlayerCommand request, CancellationToken ct)
    {
        var playerResult = Player.Create(request.Name);
        if (playerResult.IsError)
            return playerResult.Errors;
        var player = playerResult.Value;

        await playerRepository.Add(player, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return player;
    }
}
