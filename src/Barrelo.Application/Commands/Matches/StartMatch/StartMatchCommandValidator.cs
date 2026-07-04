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

            var groupSetting = factoryResult.Value.Describe().Settings.OfType<PlayerGroupSetting>().FirstOrDefault();
            if (groupSetting is null)
                return; // this game doesn't declare groups — PlayerGroups is ignored entirely

            if (command.PlayerIds.Count < 2)
                context.AddFailure(nameof(command.PlayerIds), "This game requires at least two players.");

            var groups = command.PlayerGroups ?? new Dictionary<Guid, int>();
            var missing = command.PlayerIds.Where(id => !groups.ContainsKey(id)).ToList();
            if (missing.Count > 0)
            {
                context.AddFailure(nameof(command.PlayerGroups), "Every player must be assigned to a group.");
                return;
            }

            foreach (var groupIndex in groups.Values.Distinct())
            {
                if (groupIndex < 0 || groupIndex >= groupSetting.MaxGroups)
                    context.AddFailure(
                        nameof(command.PlayerGroups),
                        $"Group index {groupIndex} is out of range (this game has {groupSetting.MaxGroups} groups).");
            }

            foreach (var perGroup in groups.Values.GroupBy(g => g))
            {
                if (perGroup.Count() > groupSetting.MaxPlayersPerGroup)
                    context.AddFailure(
                        nameof(command.PlayerGroups),
                        $"Group {perGroup.Key} has {perGroup.Count()} players; max is {groupSetting.MaxPlayersPerGroup}.");
            }
        });
    }
}
