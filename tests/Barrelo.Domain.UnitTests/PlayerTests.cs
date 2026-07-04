using Barrelo.Domain.Entities;
using Barrelo.Domain.Errors;
using FluentAssertions;

namespace Barrelo.Domain.UnitTests;

public class PlayerTests
{
    [Fact]
    public void Create_with_valid_name_succeeds()
    {
        var result = Player.Create("Febre");

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Febre");
        result.Value.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_blank_name_fails(string? name)
    {
        var result = Player.Create(name!);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(PlayerErrors.NameRequired);
    }

    [Fact]
    public void Create_trims_surrounding_whitespace()
    {
        var result = Player.Create("  Febre  ");

        result.Value.Name.Should().Be("Febre");
    }
}
