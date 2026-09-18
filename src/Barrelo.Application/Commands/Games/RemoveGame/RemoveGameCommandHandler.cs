using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using ErrorOr;

namespace Barrelo.Application.Commands.Games.RemoveGame;

public sealed class RemoveGameCommandHandler(IGameInstaller installer)
    : IRequestHandler<RemoveGameCommand, ErrorOr<Deleted>>
{
    public Task<ErrorOr<Deleted>> Handle(RemoveGameCommand request, CancellationToken ct) =>
        installer.Remove(request.GameId, ct);
}
