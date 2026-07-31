using Barrelo.GameSdk;
using FluentValidation;

namespace Barrelo.Application.Commands.Detection.RecordDetectedThrow;

public sealed class RecordDetectedThrowCommandValidator : AbstractValidator<RecordDetectedThrowCommand>
{
    public RecordDetectedThrowCommandValidator()
    {
        RuleFor(x => x.Segment).InclusiveBetween(0, 25);

        RuleFor(x => x)
            .Must(x => x.Ring is not (Ring.InnerSingle or Ring.OuterSingle or Ring.Triple) || x.Segment is >= 1 and <= 20)
            .WithMessage("A single/triple throw must specify a segment between 1 and 20.");

        RuleFor(x => x)
            .Must(x => x.Ring != Ring.Double || x.Segment is (>= 1 and <= 20) or 25)
            .WithMessage("A double throw must specify a segment between 1 and 20, or 25 for the bull.");

        RuleFor(x => x)
            .Must(x => x.Ring != Ring.Single || x.Segment == 25)
            .WithMessage("A single-bull throw must specify segment 25.");
    }
}
