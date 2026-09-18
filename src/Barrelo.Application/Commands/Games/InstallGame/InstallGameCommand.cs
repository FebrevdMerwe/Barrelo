using Barrelo.Application.Common.Dispatch;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Commands.Games.InstallGame;

public sealed record InstallGameCommand(byte[] PackageZip) : IRequest<ErrorOr<GameDescriptor>>;
