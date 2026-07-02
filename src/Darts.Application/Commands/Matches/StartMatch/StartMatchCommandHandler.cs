using System.Text.Json;
using Darts.Application.Common.Constants;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Domain.Entities;
using Darts.Domain.Enums;
using Darts.GameSdk;
using ErrorOr;
using FluentValidation;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed class StartMatchCommandHandler(
    IGameCatalog catalog,
    IPlayerRepository playerRepository,
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork,
    IGameSessionManager sessionManager,
    IValidator<StartMatchCommand> validator)
    : IRequestHandler<StartMatchCommand, ErrorOr<StartMatchResult>>
{
    public async Task<ErrorOr<StartMatchResult>> Handle(StartMatchCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return validation.Errors.Select(e => Error.Validation(e.PropertyName, e.ErrorMessage)).ToList();

        var factoryResult = catalog.Resolve(request.GameId);
        if (factoryResult.IsError)
            return factoryResult.Errors;
        var factory = factoryResult.Value;

        var players = await playerRepository.GetByIds(request.PlayerIds, ct);
        if (players.Count != request.PlayerIds.Count)
            return Error.Validation("Match.PlayersNotFound", "One or more player ids do not exist.");

        var matchResult = Match.Start(
            request.GameId,
            JsonSerializer.Serialize(request.Options),
            request.InputSource,
            request.PlayerIds);
        if (matchResult.IsError)
            return matchResult.Errors;
        var match = matchResult.Value;

        await matchRepository.Add(match, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var setup = new GameSetup(request.PlayerIds, request.Options);
        var game = await factory.Create(setup, ct);
        await sessionManager.StartSessionAsync(match.Id, game);

        if (request.InputSource == InputSource.Manual)
            sessionManager.BindBoard(WellKnownBoardIds.Manual, match.Id);

        var state = await game.GetState();
        var stamped = state with { MatchId = match.Id };

        return new StartMatchResult(match.Id, stamped);
    }
}
