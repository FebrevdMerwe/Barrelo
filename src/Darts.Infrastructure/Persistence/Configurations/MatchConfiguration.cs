using System.Text.Json;
using Darts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Darts.Infrastructure.Persistence.Configurations;

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.GameId).IsRequired().HasMaxLength(100);
        builder.Property(m => m.GameConfigJson).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();

        // WinnerPlayerIds is get-only (backed by the private _winnerPlayerIds field), stored as a
        // JSON-serialized column — consistent with GameConfigJson already being a raw JSON TEXT column
        // on this same entity, and avoids a second owned-collection table for what's just an
        // unordered, small (<=4) set of ids with no independent fields of its own.
        builder.Property(m => m.WinnerPlayerIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => (IReadOnlyList<Guid>)(JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>()))
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("WinnerPlayerIds")
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<Guid>>(
                (a, b) => (a ?? new List<Guid>()).SequenceEqual(b ?? new List<Guid>()),
                a => a.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                a => a.ToList()));

        // Participants is get-only (backed by the private _participants field), so EF must read/write
        // through the field directly rather than the property.
        builder.OwnsMany(m => m.Participants, pb =>
        {
            pb.ToTable("MatchParticipants");
            pb.WithOwner().HasForeignKey("MatchId");
            pb.HasKey("MatchId", "Order");
            pb.Property(p => p.PlayerId).IsRequired();
            pb.Property(p => p.Order).ValueGeneratedNever().IsRequired();
            pb.Property(p => p.GroupIndex).IsRequired();
            pb.Property(p => p.FinalPosition);
        });
        builder.Navigation(m => m.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
