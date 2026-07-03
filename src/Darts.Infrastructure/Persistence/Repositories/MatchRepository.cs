using Darts.Application.Common.Interfaces.Persistence;
using Darts.Domain.Entities;
using Darts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Darts.Infrastructure.Persistence.Repositories;

public sealed class MatchRepository(DartsDbContext context) : IMatchRepository
{
    public async Task Add(Match match, CancellationToken ct) => await context.Matches.AddAsync(match, ct);

    public Task<Match?> GetById(Guid id, CancellationToken ct) =>
        context.Matches.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<ThrowRecord> AddThrowRecord(
        Guid matchId,
        Guid playerId,
        int setNumber,
        int legNumber,
        int segment,
        Ring ring,
        int score,
        string rawNotation,
        double positionX,
        double positionY,
        DetectionSource source,
        DateTimeOffset detectedAtUtc,
        CancellationToken ct)
    {
        var lastSequence = await context.ThrowRecords
            .Where(t => t.MatchId == matchId)
            .Select(t => (int?)t.Sequence)
            .MaxAsync(ct) ?? 0;

        var record = ThrowRecord.Create(
            matchId, playerId, setNumber, legNumber, lastSequence + 1,
            segment, ring, score, rawNotation, positionX, positionY, source, detectedAtUtc);

        await context.ThrowRecords.AddAsync(record, ct);
        return record;
    }

    public async Task RemoveLastThrowRecord(Guid matchId, CancellationToken ct)
    {
        var last = await context.ThrowRecords
            .Where(t => t.MatchId == matchId)
            .OrderByDescending(t => t.Sequence)
            .FirstOrDefaultAsync(ct);

        if (last is not null)
            context.ThrowRecords.Remove(last);
    }

    public async Task<IReadOnlyList<ThrowRecord>> GetThrowRecords(Guid matchId, CancellationToken ct) =>
        await context.ThrowRecords.Where(t => t.MatchId == matchId).OrderBy(t => t.Sequence).ToListAsync(ct);
}
