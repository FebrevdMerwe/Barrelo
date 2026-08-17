using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using FluentValidation;

namespace Barrelo.Application.Commands.Matches.StartMatch;

public sealed class StartMatchCommandValidator : AbstractValidator<StartMatchCommand>
{
    public StartMatchCommandValidator(IGameCatalog catalog)
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerIds).NotEmpty();
        RuleFor(x => x.PlayerIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Player ids must be distinct.");

        RuleFor(x => x).Custom((command, context) =>
        {
            if (command.PlayerIds.Count == 0)
                return; // nothing to validate about groups; NotEmpty() above already reports this

            var factoryResult = catalog.Resolve(command.GameId);
            if (factoryResult.IsError)
                return; // an unknown GameId is reported by the handler's own catalog.Resolve call

            var descriptor = factoryResult.Value.Describe();

            // The floor is the game's to declare (default 1) — the host has no opinion on whether playing
            // alone makes sense. Applies to every game, grouped or not. A default floor of 1 can't fail
            // here (NotEmpty() above already covers the empty roster), so the message is always plural.
            if (command.PlayerIds.Count < descriptor.MinPlayers)
                context.AddFailure(
                    nameof(command.PlayerIds),
                    $"This game requires at least {descriptor.MinPlayers} players.");

            var groupSetting = descriptor.Settings.OfType<PlayerGroupSetting>().FirstOrDefault();
            if (groupSetting is null)
                return; // this game doesn't declare groups — PlayerGroups is ignored entirely

            var groups = command.PlayerGroups ?? new Dictionary<Guid, int>();
            var missing = command.PlayerIds.Where(id => !groups.ContainsKey(id)).ToList();
            if (missing.Count > 0)
            {
                context.AddFailure(nameof(command.PlayerGroups), "Every player must be assigned to a group.");
                return;
            }

            // Assignments for players who aren't in this match (a stale bench entry, say) are ignored:
            // only the groups actual participants sit in are checked for range, capacity and occupancy.
            var assigned = command.PlayerIds.Select(id => groups[id]).ToList();

            foreach (var groupIndex in assigned.Distinct())
            {
                if (groupIndex < 0 || groupIndex >= groupSetting.MaxGroups)
                    context.AddFailure(
                        nameof(command.PlayerGroups),
                        $"Group index {groupIndex} is out of range (this game has {groupSetting.MaxGroups} groups).");
            }

            foreach (var perGroup in assigned.GroupBy(g => g))
            {
                if (perGroup.Count() > groupSetting.MaxPlayersPerGroup)
                    context.AddFailure(
                        nameof(command.PlayerGroups),
                        $"Group {perGroup.Key} has {perGroup.Count()} players; max is {groupSetting.MaxPlayersPerGroup}.");
            }

            // Empty buckets don't count — a two-team game isn't satisfied by both players on one team.
            // As above, a floor of 1 is unreachable given a non-empty, fully assigned roster.
            var occupiedGroups = assigned.Distinct().Count();
            if (occupiedGroups < groupSetting.MinGroups)
                context.AddFailure(
                    nameof(command.PlayerGroups),
                    $"This game needs players in at least {groupSetting.MinGroups} groups; "
                        + $"this roster fills {occupiedGroups}.");
        });
    }
}
