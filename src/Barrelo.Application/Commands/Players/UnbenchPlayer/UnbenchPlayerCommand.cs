using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.UnbenchPlayer;

/// <summary>Manage Roster's "add back to board" toggle — reverses ErasePlayer's bench for a permanent player.</summary>
public sealed record UnbenchPlayerCommand(Guid PlayerId) : IRequest<ErrorOr<Success>>;
