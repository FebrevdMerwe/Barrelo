using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using ErrorOr;
using FluentValidation;

namespace Barrelo.Application.Commands.Matches.StartMatch;

public sealed class StartMatchCommandHandler(
    IGameCatalog catalog,
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore,
    IGameSessionManager sessionManager,
    IDispatcher dispatcher,
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

        IGame game;
        try
        {
            game = await factory.Create(setup, ct);
        }
        catch (GameRuleViolationException ex)
        {
            return Error.Validation("Game.RuleViolation", ex.Message);
        }
        catch (GameUnavailableException ex)
        {
            return Error.Failure("Game.Unavailable", ex.Message);
        }

        var matchId = Guid.NewGuid();
        await sessionManager.StartSessionAsync(matchId, game, playerGroupsForSetup);

        var state = await game.GetState();
        var stamped = state with { MatchId = matchId };

        var dto = MatchStateSnapshotDto.From(stamped, sessionLeaderboard: null);
        await dispatcher.Publish(new GameStateChangedEvent(matchId, dto), ct);

        return new StartMatchResult(matchId, stamped);
    }
}
