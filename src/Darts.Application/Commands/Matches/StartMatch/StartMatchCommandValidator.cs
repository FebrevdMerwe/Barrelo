using FluentValidation;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed class StartMatchCommandValidator : AbstractValidator<StartMatchCommand>
{
    public StartMatchCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerIds).NotEmpty();
        RuleFor(x => x.PlayerIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Player ids must be distinct.");
    }
}
