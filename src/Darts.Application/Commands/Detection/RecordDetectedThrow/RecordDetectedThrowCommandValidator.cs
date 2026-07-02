using Darts.GameSdk;
using FluentValidation;

namespace Darts.Application.Commands.Detection.RecordDetectedThrow;

public sealed class RecordDetectedThrowCommandValidator : AbstractValidator<RecordDetectedThrowCommand>
{
    public RecordDetectedThrowCommandValidator()
    {
        RuleFor(x => x.Segment).InclusiveBetween(0, 20);

        RuleFor(x => x)
            .Must(x => x.Ring is not (Ring.Inner or Ring.Outer or Ring.Triple or Ring.Double) || x.Segment is >= 1 and <= 20)
            .WithMessage("A single/triple/double throw must specify a segment between 1 and 20.");
    }
}
