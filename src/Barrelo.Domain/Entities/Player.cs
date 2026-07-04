using Barrelo.Domain.Common;
using Barrelo.Domain.Errors;
using ErrorOr;

namespace Barrelo.Domain.Entities;

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

    /// <summary>Reconstructs a previously-created player under its original id, e.g. to undo an erase.</summary>
    public static Player Restore(Guid id, string name, DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        Name = name,
        CreatedAtUtc = createdAtUtc,
    };
}
