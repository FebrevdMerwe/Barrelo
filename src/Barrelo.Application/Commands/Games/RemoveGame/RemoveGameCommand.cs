using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Games.RemoveGame;

public sealed record RemoveGameCommand(string GameId) : IRequest<ErrorOr<Deleted>>;
