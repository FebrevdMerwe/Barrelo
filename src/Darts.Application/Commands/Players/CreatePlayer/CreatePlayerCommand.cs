using Darts.Application.Common.Dispatch;
using Darts.Domain.Entities;
using ErrorOr;

namespace Darts.Application.Commands.Players.CreatePlayer;

public sealed record CreatePlayerCommand(string Name) : IRequest<ErrorOr<Player>>;
