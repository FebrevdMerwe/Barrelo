using Darts.Domain.Entities;
using Darts.Domain.Enums;
using Darts.Domain.Errors;
using FluentAssertions;

namespace Darts.Domain.UnitTests;

public class MatchTests
{
    [Fact]
    public void Start_with_valid_input_creates_ordered_participants()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var result = Match.Start("x01", "{}", InputSource.Manual, [p1, p2]);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(MatchStatus.InProgress);
        result.Value.Participants.Should().HaveCount(2);
        result.Value.Participants[0].PlayerId.Should().Be(p1);
        result.Value.Participants[0].Order.Should().Be(0);
        result.Value.Participants[1].PlayerId.Should().Be(p2);
        result.Value.Participants[1].Order.Should().Be(1);
    }

    [Fact]
    public void Start_with_blank_game_id_fails()
    {
        var result = Match.Start("", "{}", InputSource.Manual, [Guid.NewGuid()]);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.GameIdRequired);
    }

    [Fact]
    public void Start_with_no_participants_fails()
    {
        var result = Match.Start("x01", "{}", InputSource.Manual, []);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.NoParticipants);
    }

    [Fact]
    public void Complete_marks_the_match_completed_with_winner()
    {
        var winner = Guid.NewGuid();
        var match = Match.Start("x01", "{}", InputSource.Manual, [winner]).Value;

        var result = match.Complete(winner);

        result.IsError.Should().BeFalse();
        match.Status.Should().Be(MatchStatus.Completed);
        match.WinnerPlayerId.Should().Be(winner);
        match.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Complete_twice_fails()
    {
        var match = Match.Start("x01", "{}", InputSource.Manual, [Guid.NewGuid()]).Value;
        match.Complete(null);

        var result = match.Complete(null);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.AlreadyCompleted);
    }
}
