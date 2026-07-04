using Darts.Application.Common.Dispatch;
using ErrorOr;

namespace Darts.Application.Commands.Players.DeletePlayer;

public sealed record DeletePlayerCommand(Guid PlayerId) : IRequest<ErrorOr<Deleted>>;
