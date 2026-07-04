using Darts.Application.Common.Dispatch;
using ErrorOr;

namespace Darts.Application.Commands.Players.RestoreSessionPlayer;

/// <summary>Undoes an erase of a session-scoped player, re-inserting it under its original id.</summary>
public sealed record RestoreSessionPlayerCommand(Guid Id, string Name, DateTimeOffset CreatedAtUtc)
    : IRequest<ErrorOr<Success>>;
