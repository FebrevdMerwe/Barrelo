using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Domain.Entities;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.CreatePlayer;

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
