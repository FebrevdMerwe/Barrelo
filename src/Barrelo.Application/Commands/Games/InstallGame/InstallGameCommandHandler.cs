using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Commands.Games.InstallGame;

public sealed class InstallGameCommandHandler(IGameInstaller installer)
    : IRequestHandler<InstallGameCommand, ErrorOr<GameDescriptor>>
{
    public async Task<ErrorOr<GameDescriptor>> Handle(InstallGameCommand request, CancellationToken ct)
    {
        await using var zipStream = new MemoryStream(request.PackageZip);
        return await installer.Install(zipStream, ct);
    }
}
