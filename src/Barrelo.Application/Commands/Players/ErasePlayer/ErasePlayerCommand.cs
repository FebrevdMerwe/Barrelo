using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.ErasePlayer;

/// <summary>The chalkboard erase action — branches by player kind, see the handler.</summary>
public sealed record ErasePlayerCommand(Guid PlayerId) : IRequest<ErrorOr<Deleted>>;
