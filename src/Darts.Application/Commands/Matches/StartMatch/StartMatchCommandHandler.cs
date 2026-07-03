using System.Text.Json;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Domain.Entities;
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

        if (await sessionManager.TryGetActiveMatchIdAsync() is not null)
            return MatchSessionErrors.MatchAlreadyActive;

        var factoryResult = catalog.Resolve(request.GameId);
        if (factoryResult.IsError)
            return factoryResult.Errors;
        var factory = factoryResult.Value;

        var players = await playerRepository.GetByIds(request.PlayerIds, ct);
        if (players.Count != request.PlayerIds.Count)
            return Error.Validation("Match.PlayersNotFound", "One or more player ids do not exist.");

        // Explicit assignment if present, else own order index (own singleton group) — same fallback
        // rule as GameSetupExtensions.EffectiveGroupIndex, applied consistently to both the persisted
        // MatchParticipant.GroupIndex and the plugin-facing GameSetup.PlayerGroups.
        var groupIndexes = request.PlayerIds
            .Select((id, order) => request.PlayerGroups is not null && request.PlayerGroups.TryGetValue(id, out var groupIndex)
                ? groupIndex
                : order)
            .ToArray();
        var hasExplicitGroups = request.PlayerGroups is { Count: > 0 };

        var matchResult = Match.Start(
            request.GameId,
            JsonSerializer.Serialize(request.Options),
            request.PlayerIds,
            hasExplicitGroups ? groupIndexes : null);
        if (matchResult.IsError)
            return matchResult.Errors;
        var match = matchResult.Value;

        var playerGroupsForSetup = request.PlayerIds
            .Zip(groupIndexes, (id, groupIndex) => (id, groupIndex))
            .ToDictionary(x => x.id, x => x.groupIndex);
        var setup = new GameSetup(request.PlayerIds, request.Options, playerGroupsForSetup);
        var game = await factory.Create(setup, ct);

        if (!await sessionManager.TryStartSessionAsync(match.Id, game))
            return MatchSessionErrors.MatchAlreadyActive;

        await matchRepository.Add(match, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var state = await game.GetState();
        var stamped = state with { MatchId = match.Id };

        return new StartMatchResult(match.Id, stamped);
    }
}
