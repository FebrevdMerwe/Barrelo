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

        var result = Match.Start("x01", "{}", [p1, p2]);

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
        var result = Match.Start("", "{}", [Guid.NewGuid()]);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.GameIdRequired);
    }

    [Fact]
    public void Start_with_no_participants_fails()
    {
        var result = Match.Start("x01", "{}", []);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.NoParticipants);
    }

    [Fact]
    public void Complete_marks_the_match_completed_with_winner()
    {
        var winner = Guid.NewGuid();
        var match = Match.Start("x01", "{}", [winner]).Value;

        var result = match.Complete([winner]);

        result.IsError.Should().BeFalse();
        match.Status.Should().Be(MatchStatus.Completed);
        match.WinnerPlayerIds.Should().BeEquivalentTo([winner]);
        match.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Complete_twice_fails()
    {
        var match = Match.Start("x01", "{}", [Guid.NewGuid()]).Value;
        match.Complete([]);

        var result = match.Complete([]);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.AlreadyCompleted);
    }

    [Fact]
    public void Start_with_explicit_groups_records_group_index_per_participant()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();

        var result = Match.Start("x01", "{}", [a1, b1], [0, 1]);

        result.IsError.Should().BeFalse();
        result.Value.Participants[0].GroupIndex.Should().Be(0);
        result.Value.Participants[1].GroupIndex.Should().Be(1);
    }

    [Fact]
    public void Start_without_groups_defaults_each_participant_to_its_own_group()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var result = Match.Start("x01", "{}", [p1, p2]);

        result.IsError.Should().BeFalse();
        result.Value.Participants[0].GroupIndex.Should().Be(0);
        result.Value.Participants[1].GroupIndex.Should().Be(1);
    }

    [Fact]
    public void Start_with_mismatched_group_count_fails()
    {
        var result = Match.Start("x01", "{}", [Guid.NewGuid(), Guid.NewGuid()], [0]);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(MatchErrors.GroupAssignmentMismatch);
    }
}
