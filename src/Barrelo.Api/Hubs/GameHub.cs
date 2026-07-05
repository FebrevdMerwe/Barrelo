using Microsoft.AspNetCore.SignalR;

namespace Barrelo.Api.Hubs;

/// <summary>
/// At most one match is ever active at a time, so every connection just listens for whatever the
/// server broadcasts — no per-match group to join.
/// </summary>
public sealed class GameHub : Hub;
