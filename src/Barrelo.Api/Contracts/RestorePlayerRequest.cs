namespace Barrelo.Api.Contracts;

public sealed record RestorePlayerRequest(string Name, DateTimeOffset CreatedAtUtc);
