using Barrelo.Application.Common.Dispatch;
using Barrelo.Domain.Entities;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.CreateSessionPlayer;

public sealed record CreateSessionPlayerCommand(string Name) : IRequest<ErrorOr<Player>>;
