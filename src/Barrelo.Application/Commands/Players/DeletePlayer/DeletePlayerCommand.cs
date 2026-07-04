using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Players.DeletePlayer;

public sealed record DeletePlayerCommand(Guid PlayerId) : IRequest<ErrorOr<Deleted>>;
