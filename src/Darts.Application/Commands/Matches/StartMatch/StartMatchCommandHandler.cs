using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;
using ErrorOr;
using FluentValidation;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed class StartMatchCommandHandler(
    IGameCatalog catalog,
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore,
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

        var permanentPlayers = await playerRepository.GetByIds(request.PlayerIds, ct);
        var knownPlayerIds = permanentPlayers.Select(p => p.Id)
            .Concat(sessionPlayerStore.GetAllSessionPlayers().Select(p => p.Id))
            .ToHashSet();
        if (!request.PlayerIds.All(knownPlayerIds.Contains))
            return Error.Validation("Match.PlayersNotFound", "One or more player ids do not exist.");

        // Explicit assignment if present, else own order index (own singleton group) — same fallback
        // rule as GameSetupExtensions.EffectiveGroupIndex, applied consistently to GameSetup.PlayerGroups.
        var groupIndexes = request.PlayerIds
            .Select((id, order) => request.PlayerGroups is not null && request.PlayerGroups.TryGetValue(id, out var groupIndex)
                ? groupIndex
                : order)
            .ToArray();

        var playerGroupsForSetup = request.PlayerIds
            .Zip(groupIndexes, (id, groupIndex) => (id, groupIndex))
            .ToDictionary(x => x.id, x => x.groupIndex);
        var setup = new GameSetup(request.PlayerIds, request.Options, playerGroupsForSetup);
        var game = await factory.Create(setup, ct);

        var matchId = Guid.NewGuid();
        if (!await sessionManager.TryStartSessionAsync(matchId, game))
            return MatchSessionErrors.MatchAlreadyActive;

        var state = await game.GetState();
        var stamped = state with { MatchId = matchId };

        return new StartMatchResult(matchId, stamped);
    }
}
