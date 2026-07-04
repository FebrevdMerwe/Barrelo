using Darts.Application.Common.Dispatch;
using Darts.Domain.Entities;
using ErrorOr;

namespace Darts.Application.Commands.Players.CreateSessionPlayer;

public sealed record CreateSessionPlayerCommand(string Name) : IRequest<ErrorOr<Player>>;
