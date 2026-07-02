using Darts.Domain.Common;
using Darts.Domain.Errors;
using ErrorOr;

namespace Darts.Domain.Entities;

public sealed class Player : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Player()
    {
    }

    public static ErrorOr<Player> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return PlayerErrors.NameRequired;

        return new Player
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
