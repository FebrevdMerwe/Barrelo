using Barrelo.Application.Common.Dispatch;
using Barrelo.Domain.Entities;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.CreatePlayer;

public sealed record CreatePlayerCommand(string Name) : IRequest<ErrorOr<Player>>;
